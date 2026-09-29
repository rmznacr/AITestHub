using System.Net;
using System.Text;
using System.Text.Json;
using AITestHub.Configuration;
using AITestHub.DTOs;
using AITestHub.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// HTTP-level unit tests for EvrenTestCaseGeneratorService.
/// Uses a fake HttpMessageHandler so no real EVREN API call or network access ever happens.
/// A fake API key ("super-secret-test-key") is used to assert it never leaks into errors.
/// </summary>
public class EvrenTestCaseGeneratorServiceTests
{
    private const string FakeApiKey = "super-secret-test-key";

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }

    private static EvrenTestCaseGeneratorService CreateService(
        FakeHttpMessageHandler handler,
        string? apiKey = FakeApiKey)
    {
        var options = Options.Create(new EvrenOptions
        {
            BaseUrl = "https://evren-llmapi.ssyz.org.tr/v1",
            Model = "glm-5.3",
            ApiKey = apiKey ?? string.Empty
        });

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Value.BaseUrl)
        };

        return new EvrenTestCaseGeneratorService(
            new EvrenChatClient(client, options),
            NullLogger<EvrenTestCaseGeneratorService>.Instance);
    }

    private static string SerializeTestCases(int count) =>
        JsonSerializer.Serialize(
            Enumerable.Range(1, count).Select(i => new
            {
                title = $"Test Case {i}",
                description = $"Description {i}",
                preconditions = "Precondition.",
                steps = $"1. Step one.\n2. Step two for case {i}.",
                expectedResult = $"Expected result {i}",
                priority = i % 2 == 0 ? "High" : "Critical",
                testType = i % 3 == 0 ? "EdgeCase" : (i % 2 == 0 ? "Negative" : "Positive")
            }));

    private static HttpResponseMessage ChatResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    choices = new[]
                    {
                        new { message = new { content } }
                    }
                }),
                Encoding.UTF8,
                "application/json")
        };

    private static HttpResponseMessage ChatResponseWithRawContent(string rawBody, HttpStatusCode statusCode) =>
        new(statusCode)
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };

    private async Task<List<GeneratedTestCaseDto>> GenerateAsync(HttpResponseMessage response, int count = 2)
    {
        var handler = new FakeHttpMessageHandler(response);
        var service = CreateService(handler);
        return await service.GenerateAsync("User should be able to login.", count);
    }

    // --- Success scenarios ---

    [Fact]
    public async Task GenerateAsync_WithValidChatResponse_ShouldReturnParsedTestCases()
    {
        var result = await GenerateAsync(ChatResponse(SerializeTestCases(2)));

        Assert.Equal(2, result.Count);
        Assert.Equal("Test Case 1", result[0].Title);
        Assert.Equal("Expected result 2", result[1].ExpectedResult);
    }

    [Fact]
    public async Task GenerateAsync_WithValidChatResponse_ShouldParsePriorityAndTestType()
    {
        var result = await GenerateAsync(ChatResponse(SerializeTestCases(3)), count: 3);

        Assert.Equal(Entities.Enums.Priority.Critical, result[0].Priority);
        Assert.Equal(Entities.Enums.Priority.High, result[1].Priority);
        Assert.Equal(Entities.Enums.TestType.Positive, result[0].TestType);
        Assert.Equal(Entities.Enums.TestType.EdgeCase, result[2].TestType);
        Assert.All(result, tc => Assert.False(string.IsNullOrWhiteSpace(tc.Steps)));
        Assert.All(result, tc => Assert.NotNull(tc.Preconditions));
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidPriorityInContent_ShouldThrowConfigurationException()
    {
        var content = SerializeTestCases(1).Replace("\"Critical\"", "\"Urgent\"");

        await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(content)));
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidTestTypeInContent_ShouldThrowConfigurationException()
    {
        var content = SerializeTestCases(1).Replace("\"Positive\"", "\"Destructive\"");

        await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(content)));
    }

    [Fact]
    public async Task GenerateAsync_WithMissingStepsInContent_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "T", "description": "D", "expectedResult": "E", "priority": "High", "testType": "Positive" }
        ]
        """;

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(json), count: 1));
        Assert.Contains("steps", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WithMarkdownFencedContent_ShouldStillParseSuccessfully()
    {
        var content = $"```json\n{SerializeTestCases(2)}\n```";

        var result = await GenerateAsync(ChatResponse(content));

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GenerateAsync_ShouldSendApiKeyHeaderAndCorrectModel()
    {
        var handler = new FakeHttpMessageHandler(ChatResponse(SerializeTestCases(2)));
        var service = CreateService(handler);

        await service.GenerateAsync("Requirement", 2);

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.Contains("X-API-Key"));
        Assert.Equal(FakeApiKey, handler.LastRequest.Headers.GetValues("X-API-Key").First());
    }

    [Fact]
    public async Task GenerateAsync_WhenApiKeyIsNotConfigured_ShouldThrowConfigurationException()
    {
        var handler = new FakeHttpMessageHandler(ChatResponse(SerializeTestCases(2)));
        var service = CreateService(handler, apiKey: null);

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => service.GenerateAsync("Requirement", 2));

        Assert.Contains("Evren__ApiKey", ex.Message);
        Assert.DoesNotContain(FakeApiKey, ex.Message);
    }

    // --- Response structure scenarios ---

    [Fact]
    public async Task GenerateAsync_WhenChoicesAreEmpty_ShouldThrowConfigurationException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "choices": [] }""", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<EvrenConfigurationException>(() => GenerateAsync(response));
    }

    [Fact]
    public async Task GenerateAsync_WhenMessageContentIsMissing_ShouldThrowConfigurationException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "choices": [ { "message": {} } ] }""", Encoding.UTF8, "application/json")
        };

        await Assert.ThrowsAsync<EvrenConfigurationException>(() => GenerateAsync(response));
    }

    [Fact]
    public async Task GenerateAsync_WhenMessageContentIsEmpty_ShouldThrowConfigurationException()
    {
        await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(string.Empty)));
    }

    [Fact]
    public async Task GenerateAsync_WithMalformedJsonContent_ShouldThrowConfigurationException()
    {
        await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse("this is not json at all")));
    }

    // --- Count mismatch scenarios ---

    [Fact]
    public async Task GenerateAsync_WhenFewerTestCasesReturnedThanRequested_ShouldThrowConfigurationException()
    {
        // Requested count defaults to 2 in the helper; only 1 test case is returned.
        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(SerializeTestCases(1))));

        Assert.Contains("1", ex.Message);
        Assert.DoesNotContain("Test Case", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenMoreTestCasesReturnedThanRequested_ShouldThrowConfigurationException()
    {
        // Requested count defaults to 2 in the helper; 5 test cases are returned.
        await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(SerializeTestCases(5))));
    }

    // --- Invalid field scenarios ---

    [Fact]
    public async Task GenerateAsync_WhenTitleIsBlank_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "   ", "description": "D", "expectedResult": "E" }
        ]
        """;

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(json), count: 1));

        Assert.Contains("title", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenDescriptionIsMissing_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "T", "expectedResult": "E" }
        ]
        """;

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(json), count: 1));

        Assert.Contains("description", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenExpectedResultIsNull_ShouldThrowConfigurationException()
    {
        var json = """
        [
          { "title": "T", "description": "D", "steps": "S", "expectedResult": null, "priority": "High", "testType": "Positive" }
        ]
        """;

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(
            () => GenerateAsync(ChatResponse(json), count: 1));

        Assert.Contains("expectedResult", ex.Message);
    }

    // --- HTTP error scenarios (no API key / headers / raw body in messages) ---

    [Fact]
    public async Task GenerateAsync_WhenEvrenReturns401_ShouldThrowSafeException()
    {
        var response = ChatResponseWithRawContent("""{ "error": "invalid api key super-secret-test-key" }""", HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(() => GenerateAsync(response));

        Assert.Contains("401", ex.Message);
        Assert.DoesNotContain(FakeApiKey, ex.Message);
        Assert.DoesNotContain("invalid api key", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenEvrenReturns403_ShouldThrowSafeException()
    {
        var response = ChatResponseWithRawContent("""{ "error": "forbidden for super-secret-test-key" }""", HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(() => GenerateAsync(response));

        Assert.Contains("403", ex.Message);
        Assert.DoesNotContain(FakeApiKey, ex.Message);
        Assert.DoesNotContain("forbidden for", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenEvrenReturns429_ShouldThrowRateLimitException()
    {
        var response = ChatResponseWithRawContent("""{ "error": "rate limit exceeded" }""", HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(() => GenerateAsync(response));

        Assert.Contains("rate limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FakeApiKey, ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_WhenEvrenReturns500_ShouldThrowSafeServerException()
    {
        var response = ChatResponseWithRawContent("internal server error: super-secret-test-key", HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<EvrenConfigurationException>(() => GenerateAsync(response));

        Assert.Contains("500", ex.Message);
        Assert.DoesNotContain(FakeApiKey, ex.Message);
        Assert.DoesNotContain("internal server error: super-secret-test-key", ex.Message);
    }
}

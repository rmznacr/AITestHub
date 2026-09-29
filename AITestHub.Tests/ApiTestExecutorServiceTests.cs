using System.Net;
using System.Text;
using AITestHub.Entities;
using AITestHub.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Unit tests for ApiTestExecutorService using a fake HttpMessageHandler.
/// No real network requests are made.
/// </summary>
public class ApiTestExecutorServiceTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            LastRequest = request;

            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return _respond(request);
        }
    }

    private sealed class AllowAllValidator : IEndpointSecurityValidator
    {
        public Task<EndpointValidationResult> ValidateAsync(string endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult(EndpointValidationResult.Allowed());
    }

    private static TestCase CreateTestCase(
        string method = "GET",
        string endpoint = "https://api.example.com/users/1",
        string? body = null,
        int expectedStatusCode = 200) => new()
    {
        Id = 1,
        Name = "Test",
        HttpMethod = method,
        Endpoint = endpoint,
        RequestBody = body,
        ExpectedStatusCode = expectedStatusCode
    };

    private static (ApiTestExecutorService Service, CapturingHandler Handler) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new CapturingHandler(respond);
        var httpClient = new HttpClient(handler)
        {
            // Not the production 30 s timeout; tests rely on the handler behavior.
            Timeout = TimeSpan.FromSeconds(5)
        };
        var service = new ApiTestExecutorService(httpClient, new AllowAllValidator(), NullLogger<ApiTestExecutorService>.Instance);
        return (service, handler);
    }

    private static HttpResponseMessage Response(int statusCode, string body = "{\"ok\":true}") => new(
        (HttpStatusCode)statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    // --- Method support & pass/fail assertion ---

    [Fact]
    public async Task ExecuteAsync_GetWithMatchingStatusCode_ShouldPass()
    {
        var (service, _) = CreateService(_ => Response(200));

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.True(result.Passed);
        Assert.Equal(200, result.ActualStatusCode);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("GET", result.RequestMethod);
        Assert.Equal("https://api.example.com/users/1", result.RequestUrl);
    }

    [Fact]
    public async Task ExecuteAsync_GetWithDifferentStatusCode_ShouldFail()
    {
        var (service, _) = CreateService(_ => Response(404));

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.False(result.Passed);
        Assert.Equal(404, result.ActualStatusCode);
        Assert.Contains("Expected HTTP 200, received 404", result.ErrorMessage);
    }

    [Theory]
    [InlineData("POST", 201)]
    [InlineData("PUT", 200)]
    [InlineData("PATCH", 200)]
    [InlineData("DELETE", 204)]
    public async Task ExecuteAsync_WithSupportedMethods_ShouldSendRequestAndEvaluate(string method, int expected)
    {
        var (service, handler) = CreateService(_ => Response(expected));

        var result = await service.ExecuteAsync(CreateTestCase(method, expectedStatusCode: expected));

        Assert.Equal(method, handler.LastRequest?.Method.Method);
        Assert.True(result.Passed);
        Assert.Equal(method, result.RequestMethod);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreCaseOfHttpMethod()
    {
        var (service, handler) = CreateService(_ => Response(200));

        var result = await service.ExecuteAsync(CreateTestCase("get"));

        Assert.True(result.Passed);
        Assert.Equal("GET", result.RequestMethod);
        Assert.NotNull(handler.LastRequest);
    }

    // --- Request body handling ---

    [Fact]
    public async Task ExecuteAsync_PostWithJsonBody_ShouldSendBodyAsJson()
    {
        var (service, handler) = CreateService(_ => Response(201));
        var body = """{ "name": "test", "value": 42 }""";

        await service.ExecuteAsync(CreateTestCase("POST", body: body, expectedStatusCode: 201));

        Assert.NotNull(handler.LastRequest?.Content);
        Assert.Equal(body, handler.LastRequestBody);
        Assert.Contains("application/json", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task ExecuteAsync_GetWithRequestBody_ShouldNotSendBody()
    {
        var (service, handler) = CreateService(_ => Response(200));

        await service.ExecuteAsync(CreateTestCase("GET", body: """{ "a": 1 }"""));

        Assert.Null(handler.LastRequest?.Content);
        Assert.Null(handler.LastRequestBody);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteWithRequestBody_ShouldNotSendBody()
    {
        var (service, handler) = CreateService(_ => Response(204));

        await service.ExecuteAsync(CreateTestCase("DELETE", body: """{ "a": 1 }""", expectedStatusCode: 204));

        Assert.Null(handler.LastRequest?.Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithMalformedJsonBody_ShouldFailSafely()
    {
        var (service, handler) = CreateService(_ => Response(201));

        var result = await service.ExecuteAsync(CreateTestCase("POST", body: "{ not valid json", expectedStatusCode: 201));

        Assert.False(result.Passed);
        Assert.Contains("not valid JSON", result.ErrorMessage);
        Assert.Null(handler.LastRequest); // request was never sent
    }

    // --- Response capture ---

    [Fact]
    public async Task ExecuteAsync_ShouldCaptureResponseBody()
    {
        var (service, _) = CreateService(_ => Response(200, "{\"message\":\"hello\"}"));

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.Contains("hello", result.ResponseBody);
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeResponseBody_ShouldTruncate()
    {
        var bigBody = new string('x', ApiTestExecutorService.MaxResponseBodyLength + 5000);
        var (service, _) = CreateService(_ => Response(200, bigBody));

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.NotNull(result.ResponseBody);
        Assert.True(result.ResponseBody.Length <= ApiTestExecutorService.MaxResponseBodyLength + 100);
        Assert.Contains("truncated", result.ResponseBody);
        // Truncation must not affect the assertion:
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMeasureDuration()
    {
        var (service, _) = CreateService(_ => Response(200));

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.True(result.DurationMs >= 0);
        Assert.True(result.ExecutedAt <= DateTime.UtcNow);
    }

    // --- Safe failure scenarios ---

    [Fact]
    public async Task ExecuteAsync_WithUnsupportedMethod_ShouldFailSafely()
    {
        var (service, handler) = CreateService(_ => Response(200));

        var result = await service.ExecuteAsync(CreateTestCase("TRACE"));

        Assert.False(result.Passed);
        Assert.Contains("not supported", result.ErrorMessage);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithConnectionError_ShouldFailSafely()
    {
        var (service, _) = CreateService(_ => throw new HttpRequestException("No such host"));

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.False(result.Passed);
        Assert.NotNull(result.ErrorMessage);
        Assert.DoesNotContain("No such host", result.ErrorMessage); // no raw exception leakage
        Assert.DoesNotContain("at ", result.ErrorMessage!);          // no stack trace
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ShouldFailSafely()
    {
        // Simulates HttpClient timeout: TaskCanceledException without caller cancellation.
        var (service, _) = CreateService(_ => throw new TaskCanceledException());

        var result = await service.ExecuteAsync(CreateTestCase());

        Assert.False(result.Passed);
        Assert.Contains("timed out", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidExpectedStatusCode_ShouldFailSafely()
    {
        var (service, _) = CreateService(_ => Response(200));

        var result = await service.ExecuteAsync(CreateTestCase(expectedStatusCode: 999));

        Assert.False(result.Passed);
        Assert.Contains("not a valid HTTP status code", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancels_ShouldPropagateCancellation()
    {
        var (service, _) = CreateService(_ => Response(200));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(CreateTestCase(), cts.Token));
    }

    // --- Security integration ---

    [Fact]
    public async Task ExecuteAsync_WithBlockedEndpoint_ShouldFailSafelyWithoutSendingRequest()
    {
        var handler = new CapturingHandler(_ => Response(200));
        var httpClient = new HttpClient(handler);
        var service = new ApiTestExecutorService(
            httpClient,
            new BlockingValidator(),
            NullLogger<ApiTestExecutorService>.Instance);

        var result = await service.ExecuteAsync(CreateTestCase(endpoint: "http://192.168.1.1/api"));

        Assert.False(result.Passed);
        Assert.Contains("blocked", result.ErrorMessage);
        Assert.Null(handler.LastRequest);
    }

    private sealed class BlockingValidator : IEndpointSecurityValidator
    {
        public Task<EndpointValidationResult> ValidateAsync(string endpoint, CancellationToken cancellationToken = default) =>
            Task.FromResult(EndpointValidationResult.Blocked("The endpoint was blocked by security validation."));
    }
}

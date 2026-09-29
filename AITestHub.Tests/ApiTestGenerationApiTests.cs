using System.Net;
using System.Net.Http.Json;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Entities.Enums;
using AITestHub.Tests.Infrastructure;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Integration tests for the AI-powered executable API test generation:
/// generate-api, save-api and the generate-save-execute orchestration.
/// The AI generator and the HTTP executor are replaced by fakes; no real
/// external calls are made.
/// </summary>
public class ApiTestGenerationApiTests : IDisposable
{
    private readonly AITestHubApiFactory _factory = new();

    private HttpClient Client => _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<Project> CreateProjectAsync()
    {
        var project = new Project
        {
            Name = $"Test Project {Guid.NewGuid():N}",
            Description = "Project created by integration test."
        };

        var response = await Client.PostAsJsonAsync("/api/projects", project);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Project>(TestJsonOptions.Default))!;
    }

    private static GenerateApiTestCasesRequest CreateGenerateRequest(
        int projectId,
        int count = 2,
        string method = "POST",
        string baseUrl = "https://api.example.com",
        string endpoint = "/api/login") => new()
    {
        ProjectId = projectId,
        BaseUrl = baseUrl,
        Endpoint = endpoint,
        HttpMethod = method,
        Requirement = "Valid credentials return 200. Invalid password returns 401.",
        Count = count
    };

    private static GeneratedApiTestCaseDto CreateValidApiTestCase(
        string method = "POST",
        string endpoint = "https://api.example.com/api/users",
        string? requestBody = "{\"name\":\"Test User\",\"email\":\"test@example.com\"}",
        int expectedStatusCode = 201,
        string? requestBodyOverride = null) => new()
    {
        Title = "Create user",
        Description = "Verify that a user can be created.",
        Preconditions = "The API is reachable.",
        Steps = "1. Send the request.\n2. Check the response.",
        ExpectedResult = "The user is created.",
        Priority = Priority.High,
        TestType = TestType.Positive,
        HttpMethod = method,
        Endpoint = endpoint,
        RequestBody = requestBodyOverride ?? requestBody,
        ExpectedStatusCode = expectedStatusCode
    };

    private async Task<List<TestCase>> GetTestCasesAsync(int projectId)
    {
        return (await (await Client.GetAsync($"/api/testcases?projectId={projectId}"))
            .Content.ReadFromJsonAsync<List<TestCase>>(TestJsonOptions.Default))!;
    }

    // --- generate-api ---

    [Fact]
    public async Task GenerateApiTestCases_WithValidRequest_ShouldReturnGeneratedTests()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(project.Id, count: 3, method: "GET", endpoint: "/api/users"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<GenerateApiTestCasesResponse>(TestJsonOptions.Default))!;
        Assert.Equal(project.Id, body.ProjectId);
        Assert.Equal(3, body.TestCases.Count);
        Assert.All(body.TestCases, tc =>
        {
            Assert.Equal("GET", tc.HttpMethod);
            Assert.StartsWith("https://api.example.com/", tc.Endpoint);
            Assert.Null(tc.RequestBody);
            Assert.InRange(tc.ExpectedStatusCode, 100, 599);
        });
    }

    [Fact]
    public async Task GenerateApiTestCases_ShouldNotPersistAnything()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(project.Id));
        response.EnsureSuccessStatusCode();

        var testCases = await GetTestCasesAsync(project.Id);
        Assert.Empty(testCases);
    }

    [Fact]
    public async Task GenerateApiTestCases_WithMissingProject_ShouldReturnNotFound()
    {
        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateApiTestCases_WithInvalidBaseUrl_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(project.Id, baseUrl: "not-a-url"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateApiTestCases_WithUnsupportedMethod_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(project.Id, method: "TRACE"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task GenerateApiTestCases_WithInvalidCount_ShouldReturnBadRequest(int count)
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(project.Id, count: count));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateApiTestCases_WithBlockedBaseUrl_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-api",
            CreateGenerateRequest(project.Id, baseUrl: "http://127.0.0.1:5000"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- save-api ---

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithValidTests_ShouldPersistExecutableFields()
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto>
            {
                CreateValidApiTestCase(
                    method: "POST",
                    endpoint: "https://api.example.com/api/users",
                    requestBody: "{\"name\":\"Test User\",\"email\":\"test@example.com\"}",
                    expectedStatusCode: 201),
                CreateValidApiTestCase(
                    method: "GET",
                    endpoint: "https://api.example.com/api/users",
                    requestBody: null,
                    expectedStatusCode: 200)
            }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<SaveGeneratedTestCasesResponse>(TestJsonOptions.Default))!;
        Assert.Equal(2, body.SavedCount);

        var persisted = await GetTestCasesAsync(project.Id);
        Assert.Equal(2, persisted.Count);

        var post = persisted.Single(t => t.HttpMethod == "POST");
        Assert.Equal("https://api.example.com/api/users", post.Endpoint);
        Assert.Contains("Test User", post.RequestBody);
        Assert.Equal(201, post.ExpectedStatusCode);
        Assert.Equal(TestStatus.NotRun, post.Status);

        var get = persisted.Single(t => t.HttpMethod == "GET");
        Assert.Null(get.RequestBody);
        Assert.Equal(200, get.ExpectedStatusCode);
        Assert.Equal(TestStatus.NotRun, get.Status);
    }

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithRelativeEndpoint_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto> { CreateValidApiTestCase(endpoint: "/api/users") }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetTestCasesAsync(project.Id));
    }

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithBlockedEndpoint_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto>
            {
                CreateValidApiTestCase(endpoint: "http://169.254.169.254/latest/meta-data/")
            }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    public async Task SaveGeneratedApiTestCases_WithBodyOnBodylessMethod_ShouldReturnBadRequest(string method)
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto>
            {
                CreateValidApiTestCase(method: method, requestBody: "{\"a\":1}")
            }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithMalformedJsonBody_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto>
            {
                CreateValidApiTestCase(requestBodyOverride: "{ not valid json")
            }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithInvalidStatusCode_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto>
            {
                CreateValidApiTestCase(expectedStatusCode: 999)
            }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithOneInvalidCase_ShouldSaveNothing()
    {
        var project = await CreateProjectAsync();

        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = project.Id,
            TestCases = new List<GeneratedApiTestCaseDto>
            {
                CreateValidApiTestCase(),
                CreateValidApiTestCase(requestBodyOverride: "{ broken")
            }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetTestCasesAsync(project.Id));
    }

    [Fact]
    public async Task SaveGeneratedApiTestCases_WithMissingProject_ShouldReturnNotFound()
    {
        var request = new SaveGeneratedApiTestCasesRequest
        {
            ProjectId = 999999,
            TestCases = new List<GeneratedApiTestCaseDto> { CreateValidApiTestCase() }
        };

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save-api", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- generate-save-execute orchestration ---

    [Fact]
    public async Task GenerateSaveExecute_ShouldGenerateSaveAndExecuteTestCases()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-save-execute",
            CreateGenerateRequest(project.Id, count: 4, method: "POST", endpoint: "/api/login"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<GenerateSaveExecuteResponse>(TestJsonOptions.Default))!;
        Assert.Equal(4, body.GeneratedCount);
        Assert.Equal(4, body.SavedCount);
        Assert.Equal(4, body.ExecutedCount);
        // The mock generator marks every second case as failing; the fake executor
        // fails exactly those -> 2 passed, 2 failed. A failing case must not stop the others.
        Assert.Equal(2, body.Passed);
        Assert.Equal(2, body.Failed);
        Assert.Equal(4, body.Results.Count);
        Assert.All(body.Results, r =>
        {
            Assert.True(r.TestCaseId > 0);
            Assert.Equal("POST", r.HttpMethod);
            Assert.StartsWith("https://api.example.com/", r.Endpoint);
            Assert.True(r.DurationMs >= 0);
        });

        // Test cases persisted with updated statuses:
        var persisted = await GetTestCasesAsync(project.Id);
        Assert.Equal(4, persisted.Count);
        Assert.Equal(2, persisted.Count(t => t.Status == TestStatus.Passed));
        Assert.Equal(2, persisted.Count(t => t.Status == TestStatus.Failed));
        Assert.All(persisted, t => Assert.NotNull(t.LastExecutedAt));

        // Execution history created for every test case:
        foreach (var testCase in persisted)
        {
            var history = (await (await Client.GetAsync($"/api/testcases/{testCase.Id}/executions"))
                .Content.ReadFromJsonAsync<List<ExecutionHistoryDto>>(TestJsonOptions.Default))!;
            Assert.Single(history);
        }
    }

    [Fact]
    public async Task GenerateSaveExecute_WithMissingProject_ShouldReturnNotFound()
    {
        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-save-execute",
            CreateGenerateRequest(999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSaveExecute_WithInvalidRequest_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate-save-execute",
            CreateGenerateRequest(project.Id, method: "TRACE"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetTestCasesAsync(project.Id));
    }
}

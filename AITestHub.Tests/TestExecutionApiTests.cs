using System.Net;
using System.Net.Http.Json;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Entities.Enums;
using AITestHub.Tests.Infrastructure;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Integration tests for automated API test execution (single, batch, project,
/// history and detail endpoints). The HTTP executor is replaced by a fake, so
/// no real external requests are made.
/// </summary>
public class TestExecutionApiTests : IDisposable
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

    private async Task<TestCase> CreateExecutableTestCaseAsync(
        int projectId,
        string endpoint = "https://api.example.com/users/1",
        string method = "GET",
        int expectedStatusCode = 200,
        string? requestBody = null)
    {
        var testCase = new TestCase
        {
            Name = $"Executable Test {Guid.NewGuid():N}",
            Description = "Verifies an endpoint.",
            HttpMethod = method,
            Endpoint = endpoint,
            RequestBody = requestBody,
            ExpectedStatusCode = expectedStatusCode,
            Preconditions = "The system is accessible.",
            Steps = "1. Send the request. 2. Check the response.",
            ExpectedResult = "A successful response is returned.",
            Priority = Priority.Medium,
            TestType = TestType.Positive,
            ProjectId = projectId
        };

        var response = await Client.PostAsJsonAsync("/api/testcases", testCase);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
    }

    private async Task<TestCaseResponseDto> GetTestCaseAsync(int id)
    {
        return (await (await Client.GetAsync($"/api/testcases/{id}"))
            .Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
    }

    // --- Single execution ---

    [Fact]
    public async Task ExecuteTestCase_WithPassingAssertion_ShouldReturnOkAndPersistPassed()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);

        var response = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ExecuteTestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.True(body.Passed);
        Assert.Equal(TestStatus.Passed, body.Status);
        Assert.Equal(testCase.Id, body.TestCaseId);
        Assert.True(body.ExecutionResultId > 0);
        Assert.True(body.DurationMs >= 0);

        var fetched = await GetTestCaseAsync(testCase.Id);
        Assert.Equal(TestStatus.Passed, fetched.Status);
        Assert.NotNull(fetched.LastExecutedAt);
        Assert.NotNull(fetched.ExecutionNotes);
        Assert.Contains("Automated execution passed", fetched.ExecutionNotes);
    }

    [Fact]
    public async Task ExecuteTestCase_WithFailingAssertion_ShouldReturnOkWithPassedFalse()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id, endpoint: "https://api.example.com/fail");

        var response = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ExecuteTestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.False(body.Passed);
        Assert.Equal(TestStatus.Failed, body.Status);
        Assert.NotNull(body.ErrorMessage);
        Assert.NotEqual(testCase.ExpectedStatusCode, body.ActualStatusCode);

        var fetched = await GetTestCaseAsync(testCase.Id);
        Assert.Equal(TestStatus.Failed, fetched.Status);
    }

    [Fact]
    public async Task ExecuteTestCase_ShouldCreateExecutionHistoryEntry()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);

        await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        var history = await GetHistoryAsync(testCase.Id);
        Assert.Single(history);
        Assert.True(history[0].Passed);
    }

    [Fact]
    public async Task ExecuteTestCase_WithInvalidId_ShouldReturnNotFound()
    {
        var response = await Client.PostAsync("/api/testcases/999999/execute", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteTestCase_WithEmptyHttpMethod_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);
        testCase.HttpMethod = "";

        var update = await Client.PutAsJsonAsync($"/api/testcases/{testCase.Id}", testCase);
        update.EnsureSuccessStatusCode();

        var response = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteTestCase_WithEmptyEndpoint_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);
        testCase.Endpoint = "";

        await Client.PutAsJsonAsync($"/api/testcases/{testCase.Id}", testCase);

        var response = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteTestCase_WithInvalidExpectedStatusCode_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);
        testCase.ExpectedStatusCode = 999;

        await Client.PutAsJsonAsync($"/api/testcases/{testCase.Id}", testCase);

        var response = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteTestCase_WithBlockedEndpoint_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id, endpoint: "http://169.254.169.254/latest/meta-data/");

        var response = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- History ---

    private async Task<List<ExecutionHistoryDto>> GetHistoryAsync(int testCaseId)
    {
        return (await (await Client.GetAsync($"/api/testcases/{testCaseId}/executions"))
            .Content.ReadFromJsonAsync<List<ExecutionHistoryDto>>(TestJsonOptions.Default))!;
    }

    [Fact]
    public async Task GetExecutionHistory_AfterMultipleExecutions_ShouldReturnNewestFirst()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);

        await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);
        await Task.Delay(20); // ensure distinct ExecutedAt timestamps
        await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);

        var history = await GetHistoryAsync(testCase.Id);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].ExecutedAt >= history[1].ExecutedAt);
        Assert.All(history, h => Assert.Equal(testCase.Id, h.TestCaseId));
    }

    [Fact]
    public async Task GetExecutionHistory_WithInvalidTestCaseId_ShouldReturnNotFound()
    {
        var response = await Client.GetAsync("/api/testcases/999999/executions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Execution detail ---

    [Fact]
    public async Task GetExecutionDetail_WithValidId_ShouldReturnDetail()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id, method: "POST", requestBody: """{ "name": "x" }""");

        var execute = await Client.PostAsync($"/api/testcases/{testCase.Id}/execute", null);
        var executed = (await execute.Content.ReadFromJsonAsync<ExecuteTestCaseResponseDto>(TestJsonOptions.Default))!;

        var response = await Client.GetAsync($"/api/testexecutions/{executed.ExecutionResultId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<ExecutionDetailDto>(TestJsonOptions.Default))!;
        Assert.Equal(executed.ExecutionResultId, detail.Id);
        Assert.Equal(testCase.Id, detail.TestCaseId);
        Assert.Equal("POST", detail.RequestMethod);
        Assert.Equal(testCase.Endpoint, detail.RequestUrl);
        Assert.NotNull(detail.RequestBody);
        Assert.NotNull(detail.ResponseBody);
        Assert.True(detail.Passed);
    }

    [Fact]
    public async Task GetExecutionDetail_WithInvalidId_ShouldReturnNotFound()
    {
        var response = await Client.GetAsync("/api/testexecutions/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Batch execution ---

    [Fact]
    public async Task ExecuteBatch_WithMixedResultsAndDuplicates_ShouldDeduplicateAndCount()
    {
        var project = await CreateProjectAsync();
        var pass1 = await CreateExecutableTestCaseAsync(project.Id);
        var pass2 = await CreateExecutableTestCaseAsync(project.Id);
        var fail = await CreateExecutableTestCaseAsync(project.Id, endpoint: "https://api.example.com/fail");

        var request = new BatchExecuteRequest
        {
            TestCaseIds = new List<int> { pass1.Id, pass2.Id, fail.Id, pass1.Id, fail.Id } // duplicates
        };

        var response = await Client.PostAsJsonAsync("/api/testcases/execute-batch", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BatchExecuteResponse>(TestJsonOptions.Default))!;
        Assert.Equal(3, body.Total);
        Assert.Equal(2, body.Passed);
        Assert.Equal(1, body.Failed);
        Assert.Equal(3, body.Results.Count);
    }

    [Fact]
    public async Task ExecuteBatch_WithMissingId_ShouldNotBreakTheBatch()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateExecutableTestCaseAsync(project.Id);

        var request = new BatchExecuteRequest
        {
            TestCaseIds = new List<int> { testCase.Id, 999999 }
        };

        var response = await Client.PostAsJsonAsync("/api/testcases/execute-batch", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BatchExecuteResponse>(TestJsonOptions.Default))!;
        Assert.Equal(1, body.Total);
        Assert.Equal(1, body.Passed);
    }

    [Fact]
    public async Task ExecuteBatch_WithEmptyList_ShouldReturnBadRequest()
    {
        var response = await Client.PostAsJsonAsync("/api/testcases/execute-batch",
            new BatchExecuteRequest { TestCaseIds = new List<int>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Project execution ---

    [Fact]
    public async Task ExecuteProjectTests_ShouldRunOnlyExecutableTestsOfThatProject()
    {
        var projectA = await CreateProjectAsync();
        var projectB = await CreateProjectAsync();

        var a1 = await CreateExecutableTestCaseAsync(projectA.Id);
        var a2 = await CreateExecutableTestCaseAsync(projectA.Id, endpoint: "https://api.example.com/fail");
        var b1 = await CreateExecutableTestCaseAsync(projectB.Id);

        var response = await Client.PostAsync($"/api/projects/{projectA.Id}/execute", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BatchExecuteResponse>(TestJsonOptions.Default))!;
        Assert.Equal(2, body.Total);
        Assert.Equal(1, body.Passed);
        Assert.Equal(1, body.Failed);

        // Only project A's tests were executed:
        Assert.Equal(TestStatus.Passed, (await GetTestCaseAsync(a1.Id)).Status);
        Assert.Equal(TestStatus.Failed, (await GetTestCaseAsync(a2.Id)).Status);
        Assert.Equal(TestStatus.NotRun, (await GetTestCaseAsync(b1.Id)).Status);
    }

    [Fact]
    public async Task ExecuteProjectTests_WithNoExecutableTests_ShouldReturnEmptySummary()
    {
        var project = await CreateProjectAsync();
        // A test case without HTTP method is not executable:
        var testCase = await CreateExecutableTestCaseAsync(project.Id);
        testCase.HttpMethod = "";
        await Client.PutAsJsonAsync($"/api/testcases/{testCase.Id}", testCase);

        var response = await Client.PostAsync($"/api/projects/{project.Id}/execute", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BatchExecuteResponse>(TestJsonOptions.Default))!;
        Assert.Equal(0, body.Total);
        Assert.Equal(0, body.Passed);
        Assert.Equal(0, body.Failed);
        Assert.Empty(body.Results);
    }

    [Fact]
    public async Task ExecuteProjectTests_WithInvalidProject_ShouldReturnNotFound()
    {
        var response = await Client.PostAsync("/api/projects/999999/execute", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Summary compatibility ---

    [Fact]
    public async Task ProjectTestSummary_AfterAutomatedExecution_ShouldReflectUpdatedStatuses()
    {
        var project = await CreateProjectAsync();
        var pass = await CreateExecutableTestCaseAsync(project.Id);
        var fail = await CreateExecutableTestCaseAsync(project.Id, endpoint: "https://api.example.com/fail");

        await Client.PostAsync($"/api/projects/{project.Id}/execute", null);

        var summary = (await (await Client.GetAsync($"/api/projects/{project.Id}/test-summary"))
            .Content.ReadFromJsonAsync<ProjectTestSummaryDto>(TestJsonOptions.Default))!;

        Assert.Equal(2, summary.TotalTests);
        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(0, summary.NotRun);
        Assert.Equal(50.0, summary.PassRate);
    }
}

using System.Net;
using System.Net.Http.Json;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Entities.Enums;
using AITestHub.Tests.Infrastructure;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Integration tests for the Test Management V2 features:
/// execution, reset, project test summary and new CRUD fields.
/// </summary>
public class TestManagementV2ApiTests : IDisposable
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

    private async Task<TestCase> CreateTestCaseAsync(int projectId)
    {
        var testCase = new TestCase
        {
            Name = $"Test Case {Guid.NewGuid():N}",
            Description = "Verifies a requirement.",
            HttpMethod = "GET",
            Endpoint = "/api/users/1",
            ExpectedStatusCode = 200,
            Preconditions = "The system is accessible.",
            Steps = "1. Send the request.\n2. Check the response.",
            ExpectedResult = "A successful response is returned.",
            Priority = Priority.High,
            TestType = TestType.Positive,
            ProjectId = projectId
        };

        var response = await Client.PostAsJsonAsync("/api/testcases", testCase);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
    }

    // --- Execution endpoint ---

    [Theory]
    [InlineData(TestStatus.Passed)]
    [InlineData(TestStatus.Failed)]
    [InlineData(TestStatus.Blocked)]
    public async Task UpdateExecution_WithValidStatus_ShouldUpdateStatusNotesAndTimestamp(TestStatus status)
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateTestCaseAsync(project.Id);

        var response = await Client.PatchAsJsonAsync($"/api/testcases/{testCase.Id}/execution",
            new UpdateTestExecutionRequest { Status = status, ExecutionNotes = "Executed by test." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.Equal(status, updated.Status);
        Assert.Equal("Executed by test.", updated.ExecutionNotes);
        Assert.NotNull(updated.LastExecutedAt);
    }

    [Fact]
    public async Task UpdateExecution_WithNotRunStatus_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateTestCaseAsync(project.Id);

        var response = await Client.PatchAsJsonAsync($"/api/testcases/{testCase.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.NotRun });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExecution_WhenTestCaseDoesNotExist_ShouldReturnNotFound()
    {
        var response = await Client.PatchAsJsonAsync("/api/testcases/999999/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Passed });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExecution_ShouldPersistExecutionData()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateTestCaseAsync(project.Id);

        await Client.PatchAsJsonAsync($"/api/testcases/{testCase.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Failed, ExecutionNotes = "Assertion failed." });

        var fetched = (await (await Client.GetAsync($"/api/testcases/{testCase.Id}"))
            .Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.Equal(TestStatus.Failed, fetched.Status);
        Assert.Equal("Assertion failed.", fetched.ExecutionNotes);
        Assert.NotNull(fetched.LastExecutedAt);
    }

    // --- Reset endpoint ---

    [Fact]
    public async Task ResetExecution_AfterExecution_ShouldResetStatusNotesAndTimestamp()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateTestCaseAsync(project.Id);

        await Client.PatchAsJsonAsync($"/api/testcases/{testCase.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Failed, ExecutionNotes = "Some notes." });

        var response = await Client.PatchAsync($"/api/testcases/{testCase.Id}/reset", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reset = (await response.Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.Equal(TestStatus.NotRun, reset.Status);
        Assert.Null(reset.ExecutionNotes);
        Assert.Null(reset.LastExecutedAt);
    }

    [Fact]
    public async Task ResetExecution_WhenTestCaseDoesNotExist_ShouldReturnNotFound()
    {
        var response = await Client.PatchAsync("/api/testcases/999999/reset", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Project test summary endpoint ---

    [Fact]
    public async Task GetProjectTestSummary_ShouldReturnCorrectCountsAndPassRate()
    {
        var project = await CreateProjectAsync();
        var testCases = new List<TestCase>();
        for (var i = 0; i < 5; i++)
        {
            testCases.Add(await CreateTestCaseAsync(project.Id));
        }

        // 2 passed, 1 failed, 1 blocked, 1 not run -> PassRate = 2/4*100 = 50
        await Client.PatchAsJsonAsync($"/api/testcases/{testCases[0].Id}/execution", new UpdateTestExecutionRequest { Status = TestStatus.Passed });
        await Client.PatchAsJsonAsync($"/api/testcases/{testCases[1].Id}/execution", new UpdateTestExecutionRequest { Status = TestStatus.Passed });
        await Client.PatchAsJsonAsync($"/api/testcases/{testCases[2].Id}/execution", new UpdateTestExecutionRequest { Status = TestStatus.Failed });
        await Client.PatchAsJsonAsync($"/api/testcases/{testCases[3].Id}/execution", new UpdateTestExecutionRequest { Status = TestStatus.Blocked });

        var response = await Client.GetAsync($"/api/projects/{project.Id}/test-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<ProjectTestSummaryDto>(TestJsonOptions.Default))!;
        Assert.Equal(project.Id, summary.ProjectId);
        Assert.Equal(project.Name, summary.ProjectName);
        Assert.Equal(5, summary.TotalTests);
        Assert.Equal(2, summary.Passed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Blocked);
        Assert.Equal(1, summary.NotRun);
        Assert.Equal(50.0, summary.PassRate);
    }

    [Fact]
    public async Task GetProjectTestSummary_WhenNoTestExecuted_ShouldReturnZeroPassRate()
    {
        var project = await CreateProjectAsync();
        await CreateTestCaseAsync(project.Id);
        await CreateTestCaseAsync(project.Id);

        var response = await Client.GetAsync($"/api/projects/{project.Id}/test-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<ProjectTestSummaryDto>(TestJsonOptions.Default))!;
        Assert.Equal(2, summary.TotalTests);
        Assert.Equal(2, summary.NotRun);
        Assert.Equal(0, summary.Passed);
        Assert.Equal(0, summary.PassRate);
    }

    [Fact]
    public async Task GetProjectTestSummary_WhenProjectDoesNotExist_ShouldReturnNotFound()
    {
        var response = await Client.GetAsync("/api/projects/999999/test-summary");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- CRUD with new fields ---

    [Fact]
    public async Task TestCaseCrud_WithNewFields_ShouldCreateReadAndUpdateCorrectly()
    {
        var project = await CreateProjectAsync();

        var create = new TestCase
        {
            Name = "V2 CRUD Test Case",
            Description = "Description.",
            Preconditions = "Preconditions.",
            Steps = "1. Step.\n2. Step.",
            ExpectedResult = "Expected.",
            Priority = Priority.Critical,
            TestType = TestType.EdgeCase,
            HttpMethod = "POST",
            Endpoint = "/api/users",
            ExpectedStatusCode = 201,
            ProjectId = project.Id
        };

        var createResponse = await Client.PostAsJsonAsync("/api/testcases", create);
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.Equal(Priority.Critical, created.Priority);
        Assert.Equal(TestType.EdgeCase, created.TestType);
        Assert.Equal("1. Step.\n2. Step.", created.Steps);
        Assert.Equal("Preconditions.", created.Preconditions);
        Assert.Equal(TestStatus.NotRun, created.Status);

        var update = new TestCase
        {
            Id = created.Id,
            Name = "V2 CRUD Test Case",
            Description = "Updated description.",
            Preconditions = "Updated preconditions.",
            Steps = "1. Updated step.",
            ExpectedResult = "Updated expected result.",
            Priority = Priority.Low,
            TestType = TestType.Negative,
            HttpMethod = "POST",
            Endpoint = "/api/users",
            ExpectedStatusCode = 201,
            ProjectId = project.Id
        };

        var updateResponse = await Client.PutAsJsonAsync($"/api/testcases/{created.Id}", update);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var fetched = (await (await Client.GetAsync($"/api/testcases/{created.Id}"))
            .Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.Equal(Priority.Low, fetched.Priority);
        Assert.Equal(TestType.Negative, fetched.TestType);
        Assert.Equal("1. Updated step.", fetched.Steps);
        Assert.Equal("Updated preconditions.", fetched.Preconditions);
    }

    [Fact]
    public async Task TestCasePut_ShouldNotChangeExecutionState()
    {
        var project = await CreateProjectAsync();
        var testCase = await CreateTestCaseAsync(project.Id);

        await Client.PatchAsJsonAsync($"/api/testcases/{testCase.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Passed, ExecutionNotes = "Passed before update." });

        // A PUT that (accidentally) tries to change execution fields must not affect them.
        var put = new TestCase
        {
            Id = testCase.Id,
            Name = testCase.Name,
            Description = testCase.Description,
            Steps = testCase.Steps,
            HttpMethod = testCase.HttpMethod,
            Endpoint = testCase.Endpoint,
            ExpectedStatusCode = testCase.ExpectedStatusCode,
            ProjectId = project.Id,
            Status = TestStatus.Failed,
            ExecutionNotes = "Should be ignored.",
            LastExecutedAt = DateTime.MinValue
        };

        var response = await Client.PutAsJsonAsync($"/api/testcases/{testCase.Id}", put);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = (await (await Client.GetAsync($"/api/testcases/{testCase.Id}"))
            .Content.ReadFromJsonAsync<TestCaseResponseDto>(TestJsonOptions.Default))!;
        Assert.Equal(TestStatus.Passed, fetched.Status);
        Assert.Equal("Passed before update.", fetched.ExecutionNotes);
    }
}

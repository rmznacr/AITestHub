using System.Net;
using System.Net.Http.Json;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Tests.Infrastructure;

namespace AITestHub.Tests;

public class AITestCasesSaveApiTests : IDisposable
{
    // One factory (and therefore one fresh, private InMemory database) per test instance.
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

    private static SaveGeneratedTestCasesRequest ValidSaveRequest(int projectId, int testCaseCount = 2) => new()
    {
        ProjectId = projectId,
        TestCases = Enumerable.Range(1, testCaseCount).Select(i => new GeneratedTestCaseDto
        {
            Title = $"Test Case {i}",
            Description = $"Description {i}",
            Preconditions = $"Precondition {i}",
            Steps = $"1. Step one.\n2. Step two for case {i}.",
            ExpectedResult = $"Expected result {i}",
            Priority = Entities.Enums.Priority.High,
            TestType = i % 2 == 0 ? Entities.Enums.TestType.Negative : Entities.Enums.TestType.Positive
        }).ToList()
    };

    [Fact]
    public async Task SaveGeneratedTestCases_WithValidProjectAndTestCases_ShouldReturnOkWithSavedCount()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", ValidSaveRequest(project.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<SaveGeneratedTestCasesResponse>(TestJsonOptions.Default))!;
        Assert.Equal(project.Id, body.ProjectId);
        Assert.Equal(2, body.SavedCount);
        Assert.Equal(2, body.TestCases.Count);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_ShouldPersistTestCasesInDatabase()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id);

        var saveResponse = await Client.PostAsJsonAsync("/api/aitestcases/save", request);
        saveResponse.EnsureSuccessStatusCode();
        var saved = (await saveResponse.Content.ReadFromJsonAsync<SaveGeneratedTestCasesResponse>(TestJsonOptions.Default))!;

        // Verify via the existing test cases API that the records are actually in the database.
        foreach (var testCase in saved.TestCases)
        {
            var getResponse = await Client.GetAsync($"/api/testcases/{testCase.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = (await getResponse.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
            Assert.Equal(testCase.Name, fetched.Name);
            Assert.Equal(testCase.ExpectedResult, fetched.ExpectedResult);
        }
    }

    [Fact]
    public async Task SaveGeneratedTestCases_ShouldAssignCorrectProjectId()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", ValidSaveRequest(project.Id));
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<SaveGeneratedTestCasesResponse>(TestJsonOptions.Default))!;

        Assert.All(saved.TestCases, tc => Assert.Equal(project.Id, tc.ProjectId));

        // Cross-check via the project-scoped filter endpoint.
        var filterResponse = await Client.GetAsync($"/api/testcases?projectId={project.Id}");
        var filtered = (await filterResponse.Content.ReadFromJsonAsync<List<TestCase>>(TestJsonOptions.Default))!;
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_ShouldPersistNewFieldsAndDefaultStatus()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id);

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);
        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<SaveGeneratedTestCasesResponse>(TestJsonOptions.Default))!;

        Assert.All(saved.TestCases, tc =>
        {
            Assert.Equal(Entities.Enums.TestStatus.NotRun, tc.Status);
            Assert.Equal(Entities.Enums.Priority.High, tc.Priority);
            Assert.False(string.IsNullOrWhiteSpace(tc.Steps));
            Assert.False(string.IsNullOrWhiteSpace(tc.Preconditions));
            Assert.Null(tc.ExecutionNotes);
            Assert.Null(tc.LastExecutedAt);
        });
    }

    [Fact]
    public async Task SaveGeneratedTestCases_WhenProjectDoesNotExist_ShouldReturnNotFound()
    {
        var request = ValidSaveRequest(999999);

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_WithEmptyTestCasesList_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id, testCaseCount: 0);

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_WithMoreThanTwentyTestCases_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id, testCaseCount: 21);

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_WhenTitleIsEmpty_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id, testCaseCount: 1);
        request.TestCases[0].Title = "";

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_WhenDescriptionIsEmpty_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id, testCaseCount: 1);
        request.TestCases[0].Description = "";

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SaveGeneratedTestCases_WhenExpectedResultIsEmpty_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidSaveRequest(project.Id, testCaseCount: 1);
        request.TestCases[0].ExpectedResult = "";

        var response = await Client.PostAsJsonAsync("/api/aitestcases/save", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateEndpoint_ShouldNotPersistTestCases()
    {
        var project = await CreateProjectAsync();

        var generateResponse = await Client.PostAsJsonAsync("/api/aitestcases/generate", new GenerateTestCasesRequest
        {
            ProjectId = project.Id,
            Requirement = "User should be able to login with valid credentials.",
            Count = 3
        });
        generateResponse.EnsureSuccessStatusCode();

        var filterResponse = await Client.GetAsync($"/api/testcases?projectId={project.Id}");
        var filtered = (await filterResponse.Content.ReadFromJsonAsync<List<TestCase>>(TestJsonOptions.Default))!;
        Assert.Empty(filtered);
    }
}



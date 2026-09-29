using System.Net;
using System.Net.Http.Json;
using AITestHub.Entities;
using AITestHub.Tests.Infrastructure;

namespace AITestHub.Tests;

public class TestCasesApiTests : IDisposable
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

    private async Task<TestCase> CreateTestCaseAsync(int projectId)
    {
        var testCase = new TestCase
        {
            Name = $"Test Case {Guid.NewGuid():N}",
            Description = "Verifies that the endpoint returns a successful response.",
            HttpMethod = "GET",
            Endpoint = "/api/users/1",
            RequestBody = null,
            ExpectedStatusCode = 200,
            ProjectId = projectId
        };

        var response = await Client.PostAsJsonAsync("/api/testcases", testCase);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
    }

    [Fact]
    public async Task CreateTestCase_WithValidProject_ShouldReturnCreated()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/testcases", new TestCase
        {
            Name = $"Test Case {Guid.NewGuid():N}",
            HttpMethod = "POST",
            Endpoint = "/api/users",
            RequestBody = """{ "name": "user" }""",
            ExpectedStatusCode = 201,
            ProjectId = project.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
        Assert.True(created.Id > 0);
        Assert.Equal(project.Id, created.ProjectId);
    }

    [Fact]
    public async Task CreateTestCase_WithNonExistingProject_ShouldReturnNotFound()
    {
        var response = await Client.PostAsJsonAsync("/api/testcases", new TestCase
        {
            Name = $"Test Case {Guid.NewGuid():N}",
            HttpMethod = "GET",
            Endpoint = "/api/users/1",
            ExpectedStatusCode = 200,
            ProjectId = 999999
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTestCase_WhenTestCaseExists_ShouldReturnTestCase()
    {
        var project = await CreateProjectAsync();
        var created = await CreateTestCaseAsync(project.Id);

        var response = await Client.GetAsync($"/api/testcases/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = (await response.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Name, fetched.Name);
        Assert.Equal(project.Id, fetched.ProjectId);
    }

    [Fact]
    public async Task GetTestCases_WithProjectIdFilter_ShouldReturnOnlyTestCasesOfThatProject()
    {
        var projectA = await CreateProjectAsync();
        var projectB = await CreateProjectAsync();

        var testCaseA1 = await CreateTestCaseAsync(projectA.Id);
        var testCaseA2 = await CreateTestCaseAsync(projectA.Id);
        await CreateTestCaseAsync(projectB.Id);

        var response = await Client.GetAsync($"/api/testcases?projectId={projectA.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var testCases = (await response.Content.ReadFromJsonAsync<List<TestCase>>(TestJsonOptions.Default))!;
        Assert.Equal(2, testCases.Count);
        Assert.All(testCases, t => Assert.Equal(projectA.Id, t.ProjectId));
        Assert.Contains(testCases, t => t.Id == testCaseA1.Id);
        Assert.Contains(testCases, t => t.Id == testCaseA2.Id);
    }

    [Fact]
    public async Task UpdateTestCase_ShouldUpdateTestCase()
    {
        var project = await CreateProjectAsync();
        var created = await CreateTestCaseAsync(project.Id);

        var updated = new TestCase
        {
            Id = created.Id,
            Name = $"Updated Test Case {Guid.NewGuid():N}",
            Description = "Updated description.",
            HttpMethod = "PUT",
            Endpoint = "/api/users/1",
            RequestBody = """{ "name": "updated" }""",
            ExpectedStatusCode = 204,
            ProjectId = project.Id
        };

        var response = await Client.PutAsJsonAsync($"/api/testcases/{created.Id}", updated);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = (await (await Client.GetAsync($"/api/testcases/{created.Id}"))
            .Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
        Assert.Equal(updated.Name, fetched.Name);
        Assert.Equal(updated.HttpMethod, fetched.HttpMethod);
        Assert.Equal(updated.Endpoint, fetched.Endpoint);
        Assert.Equal(updated.ExpectedStatusCode, fetched.ExpectedStatusCode);
    }

    [Fact]
    public async Task DeleteTestCase_ShouldRemoveTestCase()
    {
        var project = await CreateProjectAsync();
        var created = await CreateTestCaseAsync(project.Id);

        var response = await Client.DeleteAsync($"/api/testcases/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/testcases/{created.Id}")).StatusCode);
    }
}



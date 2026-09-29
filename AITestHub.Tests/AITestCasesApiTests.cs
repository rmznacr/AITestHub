using System.Net;
using System.Net.Http.Json;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Tests.Infrastructure;

namespace AITestHub.Tests;

public class AITestCasesApiTests : IDisposable
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

    private static GenerateTestCasesRequest ValidRequest(int projectId) => new()
    {
        ProjectId = projectId,
        Requirement = "User should be able to login with valid credentials.",
        Count = 5
    };

    [Fact]
    public async Task GenerateTestCases_WithValidProjectAndRequirement_ShouldReturnOk()
    {
        var project = await CreateProjectAsync();

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate", ValidRequest(project.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<GenerateTestCasesResponse>(TestJsonOptions.Default))!;
        Assert.Equal(project.Id, body.ProjectId);
        Assert.Equal("User should be able to login with valid credentials.", body.Requirement);
        Assert.NotEmpty(body.TestCases);
    }

    [Fact]
    public async Task GenerateTestCases_WithValidRequest_ShouldReturnRequestedNumberOfTestCases()
    {
        var project = await CreateProjectAsync();
        var request = ValidRequest(project.Id);
        request.Count = 7;

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<GenerateTestCasesResponse>(TestJsonOptions.Default))!;
        Assert.Equal(7, body.TestCases.Count);
        Assert.All(body.TestCases, tc =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tc.Title));
            Assert.False(string.IsNullOrWhiteSpace(tc.Description));
            Assert.False(string.IsNullOrWhiteSpace(tc.ExpectedResult));
        });
    }

    [Fact]
    public async Task GenerateTestCases_WhenProjectDoesNotExist_ShouldReturnNotFound()
    {
        var request = ValidRequest(999999);

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTestCases_WithEmptyRequirement_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidRequest(project.Id);
        request.Requirement = "";

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTestCases_WithCountZero_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidRequest(project.Id);
        request.Count = 0;

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateTestCases_WithCountAboveTwenty_ShouldReturnBadRequest()
    {
        var project = await CreateProjectAsync();
        var request = ValidRequest(project.Id);
        request.Count = 21;

        var response = await Client.PostAsJsonAsync("/api/aitestcases/generate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}



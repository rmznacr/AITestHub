using System.Net;
using System.Net.Http.Json;
using AITestHub.Entities;
using AITestHub.Tests.Infrastructure;

namespace AITestHub.Tests;

public class ProjectsApiTests : IDisposable
{
    // One factory (and therefore one fresh, private InMemory database) per test instance.
    private readonly AITestHubApiFactory _factory = new();

    private HttpClient Client => _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private static Project NewProject(string? name = null) => new()
    {
        Name = name ?? $"Test Project {Guid.NewGuid():N}",
        Description = "Project created by integration test."
    };

    private async Task<Project> CreateProjectAsync(string? name = null)
    {
        var response = await Client.PostAsJsonAsync("/api/projects", NewProject(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Project>())!;
    }

    [Fact]
    public async Task CreateProject_ShouldReturnCreated()
    {
        var project = NewProject();

        var response = await Client.PostAsJsonAsync("/api/projects", project);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<Project>())!;
        Assert.True(created.Id > 0);
        Assert.Equal(project.Name, created.Name);
        Assert.NotEqual(default, created.CreatedAt);
    }

    [Fact]
    public async Task GetProjects_WhenProjectsExist_ShouldReturnProjectList()
    {
        var created = await CreateProjectAsync();

        var response = await Client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = (await response.Content.ReadFromJsonAsync<List<Project>>())!;
        Assert.Contains(projects, p => p.Id == created.Id && p.Name == created.Name);
    }

    [Fact]
    public async Task GetProject_WhenProjectExists_ShouldReturnProject()
    {
        var created = await CreateProjectAsync();

        var response = await Client.GetAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = (await response.Content.ReadFromJsonAsync<Project>())!;
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Name, fetched.Name);
    }

    [Fact]
    public async Task GetProject_WhenProjectDoesNotExist_ShouldReturnNotFound()
    {
        var response = await Client.GetAsync("/api/projects/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_ShouldUpdateProject()
    {
        var created = await CreateProjectAsync();

        var updated = new Project
        {
            Id = created.Id,
            Name = $"Updated Project {Guid.NewGuid():N}",
            Description = "Updated description."
        };

        var response = await Client.PutAsJsonAsync($"/api/projects/{created.Id}", updated);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var fetched = (await (await Client.GetAsync($"/api/projects/{created.Id}"))
            .Content.ReadFromJsonAsync<Project>())!;
        Assert.Equal(updated.Name, fetched.Name);
        Assert.Equal(updated.Description, fetched.Description);
    }

    [Fact]
    public async Task DeleteProject_ShouldRemoveProject()
    {
        var created = await CreateProjectAsync();

        var response = await Client.DeleteAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/projects/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task GetProject_AfterDelete_ShouldReturnNotFound()
    {
        var created = await CreateProjectAsync();
        await Client.DeleteAsync($"/api/projects/{created.Id}");

        var response = await Client.GetAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Entities.Enums;
using AITestHub.Tests.Infrastructure;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Integration tests for the global dashboard summary endpoint.
/// </summary>
public class DashboardApiTests : IDisposable
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
            Endpoint = "https://api.example.com/users/1",
            ExpectedStatusCode = 200,
            Steps = "1. Send the request.",
            ProjectId = projectId
        };

        var response = await Client.PostAsJsonAsync("/api/testcases", testCase);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TestCase>(TestJsonOptions.Default))!;
    }

    [Fact]
    public async Task GetDashboardSummary_WithNoData_ShouldReturnZeroes()
    {
        var summary = (await (await Client.GetAsync("/api/dashboard/summary"))
            .Content.ReadFromJsonAsync<DashboardSummaryDto>(TestJsonOptions.Default))!;

        Assert.Equal(0, summary.TotalProjects);
        Assert.Equal(0, summary.TotalTests);
        Assert.Equal(0, summary.Passed);
        Assert.Equal(0, summary.PassRate);
    }

    [Fact]
    public async Task GetDashboardSummary_WithProjectsAndTests_ShouldReturnAggregatedCounts()
    {
        var projectA = await CreateProjectAsync();
        var projectB = await CreateProjectAsync();

        var t1 = await CreateTestCaseAsync(projectA.Id);
        var t2 = await CreateTestCaseAsync(projectA.Id);
        var t3 = await CreateTestCaseAsync(projectB.Id);

        await Client.PatchAsJsonAsync($"/api/testcases/{t1.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Passed });
        await Client.PatchAsJsonAsync($"/api/testcases/{t2.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Failed });
        await Client.PatchAsJsonAsync($"/api/testcases/{t3.Id}/execution",
            new UpdateTestExecutionRequest { Status = TestStatus.Blocked });

        var summary = (await (await Client.GetAsync("/api/dashboard/summary"))
            .Content.ReadFromJsonAsync<DashboardSummaryDto>(TestJsonOptions.Default))!;

        Assert.Equal(2, summary.TotalProjects);
        Assert.Equal(3, summary.TotalTests);
        Assert.Equal(1, summary.Passed);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(1, summary.Blocked);
        Assert.Equal(0, summary.NotRun);
        Assert.Equal(33.33, summary.PassRate);
    }
}

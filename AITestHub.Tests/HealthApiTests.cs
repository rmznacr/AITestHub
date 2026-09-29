using System.Net;
using System.Net.Http.Json;
using AITestHub.Tests.Infrastructure;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Integration tests for the /health endpoint used by Docker healthchecks.
/// </summary>
public class HealthApiTests : IDisposable
{
    private readonly AITestHubApiFactory _factory = new();

    private HttpClient Client => _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Health_WhenDatabaseIsReachable_ShouldReturnHealthy()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("Healthy", body!.Status);
        Assert.Equal("Connected", body.Database);
    }

    private sealed class HealthResponse
    {
        public string? Status { get; set; }
        public string? Database { get; set; }
    }
}

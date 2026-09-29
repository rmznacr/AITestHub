using AITestHub.Services;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Unit tests for the deterministic BaseUrl + Endpoint URL normalizer.
/// </summary>
public class UrlNormalizerTests
{
    private readonly UrlNormalizer _normalizer = new();

    [Theory]
    [InlineData("https://example.com", "/api/users", "https://example.com/api/users")]
    [InlineData("https://example.com/", "/api/users", "https://example.com/api/users")]
    [InlineData("https://example.com", "api/users", "https://example.com/api/users")]
    [InlineData("https://example.com/", "api/users", "https://example.com/api/users")]
    [InlineData("https://example.com/v1", "/api/users", "https://example.com/v1/api/users")]
    [InlineData("https://example.com/v1/", "api/users", "https://example.com/v1/api/users")]
    [InlineData("http://example.com", "/api/users", "http://example.com/api/users")]
    [InlineData("https://example.com", "/api/users?page=1&size=10", "https://example.com/api/users?page=1&size=10")]
    [InlineData("https://example.com", "api/users?id=5", "https://example.com/api/users?id=5")]
    public void Normalize_WithRelativeEndpoint_ShouldCombineCorrectly(string baseUrl, string endpoint, string expected)
    {
        var result = _normalizer.Normalize(baseUrl, endpoint);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Url);
    }

    [Theory]
    [InlineData("https://example.com", "https://example.com/api/users", "https://example.com/api/users")]
    [InlineData("https://example.com", "https://example.com/api/users?page=1", "https://example.com/api/users?page=1")]
    public void Normalize_WithAbsoluteEndpointOnSameHost_ShouldAccept(string baseUrl, string endpoint, string expected)
    {
        var result = _normalizer.Normalize(baseUrl, endpoint);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Url);
    }

    [Fact]
    public void Normalize_WithAbsoluteEndpointOnDifferentHost_ShouldReject()
    {
        var result = _normalizer.Normalize("https://example.com", "https://evil.example.org/api/users");

        Assert.False(result.Success);
        Assert.Contains("host", result.Error);
    }

    [Fact]
    public void Normalize_WithSubdomainOfDifferentHost_ShouldReject()
    {
        var result = _normalizer.Normalize("https://example.com", "https://api.example.com/login");

        Assert.False(result.Success);
    }

    [Fact]
    public void Normalize_WithSchemeDowngrade_ShouldReject()
    {
        var result = _normalizer.Normalize("https://example.com", "http://example.com/api/users");

        Assert.False(result.Success);
        Assert.Contains("scheme", result.Error);
    }

    [Fact]
    public void Normalize_WithSchemeUpgrade_ShouldReject()
    {
        var result = _normalizer.Normalize("http://example.com", "https://example.com/api/users");

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("/relative/base")]
    public void Normalize_WithInvalidBaseUrl_ShouldReject(string baseUrl)
    {
        var result = _normalizer.Normalize(baseUrl, "/api/users");

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_WithEmptyEndpoint_ShouldReject(string endpoint)
    {
        var result = _normalizer.Normalize("https://example.com", endpoint);

        Assert.False(result.Success);
    }

    [Fact]
    public void Normalize_WithUnsupportedEndpointScheme_ShouldReject()
    {
        var result = _normalizer.Normalize("https://example.com", "file:///c:/windows/win.ini");

        Assert.False(result.Success);
    }
}

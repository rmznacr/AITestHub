using System.Net;
using System.Net.Sockets;
using AITestHub.Services;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Unit tests for the SSRF protection of EndpointSecurityValidator.
/// Uses a fake DNS resolver; no real network access happens in these tests.
/// </summary>
public class EndpointSecurityValidatorTests
{
    private static EndpointSecurityValidator CreateValidator(Func<string, IPAddress[]>? resolve = null)
    {
        resolve ??= _ => new[] { IPAddress.Parse("93.184.216.34") };
        return new EndpointSecurityValidator(new FakeResolver(resolve));
    }

    private sealed class FakeResolver : IDnsResolver
    {
        private readonly Func<string, IPAddress[]> _resolve;

        public FakeResolver(Func<string, IPAddress[]> resolve) => _resolve = resolve;

        public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken = default) =>
            Task.FromResult(_resolve(host));
    }

    // --- Blocked: literal local/private addresses ---

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:5000/api")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.2")]
    [InlineData("http://127.255.255.255")]
    [InlineData("http://10.0.0.1")]
    [InlineData("http://10.255.255.255")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://192.168.0.100")]
    [InlineData("http://169.254.169.254")] // cloud metadata endpoint
    [InlineData("http://169.254.0.1")]
    [InlineData("http://[::1]")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[fc00::1]")]
    [InlineData("http://[fd12:3456::1]")]
    [InlineData("http://[::ffff:127.0.0.1]")]   // IPv4-mapped IPv6 bypass attempt
    [InlineData("http://[::ffff:10.0.0.1]")]    // IPv4-mapped IPv6 bypass attempt
    [InlineData("http://[::ffff:192.168.1.1]")] // IPv4-mapped IPv6 bypass attempt
    [InlineData("http://0.0.0.0")]
    public async Task ValidateAsync_WithLocalOrPrivateEndpoint_ShouldBeBlocked(string endpoint)
    {
        var result = await CreateValidator().ValidateAsync(endpoint);

        Assert.False(result.IsAllowed);
        Assert.False(string.IsNullOrEmpty(result.Reason));
    }

    // --- Blocked: non-http schemes ---

    [Theory]
    [InlineData("file:///c:/windows/win.ini")]
    [InlineData("ftp://example.com/file")]
    [InlineData("gopher://example.com")]
    public async Task ValidateAsync_WithUnsupportedScheme_ShouldBeBlocked(string endpoint)
    {
        var result = await CreateValidator().ValidateAsync(endpoint);

        Assert.False(result.IsAllowed);
    }

    // --- Blocked: malformed endpoints ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("/api/relative")]
    public async Task ValidateAsync_WithInvalidUrl_ShouldBeBlocked(string endpoint)
    {
        var result = await CreateValidator().ValidateAsync(endpoint);

        Assert.False(result.IsAllowed);
    }

    // --- Blocked: hostname resolves to a private address (DNS rebinding style) ---

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.0.1")]
    [InlineData("172.20.1.2")]
    [InlineData("169.254.169.254")]
    [InlineData("127.0.0.1")]
    public async Task ValidateAsync_WhenHostResolvesToPrivateAddress_ShouldBeBlocked(string resolvedIp)
    {
        var validator = CreateValidator(_ => new[] { IPAddress.Parse(resolvedIp) });

        var result = await validator.ValidateAsync("https://api.example.com/users");

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateAsync_WhenOneResolvedAddressIsPrivate_ShouldBeBlocked()
    {
        var validator = CreateValidator(_ => new[]
        {
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("10.0.0.7")
        });

        var result = await validator.ValidateAsync("https://multi.example.com");

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateAsync_WhenDnsResolutionFails_ShouldBeBlocked()
    {
        var validator = CreateValidator(_ => throw new SocketException());

        var result = await validator.ValidateAsync("https://unresolvable.example.com");

        Assert.False(result.IsAllowed);
    }

    // --- Allowed: public endpoints ---

    [Theory]
    [InlineData("https://jsonplaceholder.typicode.com/posts/1")]
    [InlineData("http://93.184.216.34/api")]
    [InlineData("https://api.example.com:8443/users?page=1")]
    public async Task ValidateAsync_WithPublicEndpoint_ShouldBeAllowed(string endpoint)
    {
        var result = await CreateValidator().ValidateAsync(endpoint);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task ValidateAsync_WhenAllResolvedAddressesArePublic_ShouldBeAllowed()
    {
        var validator = CreateValidator(_ => new[]
        {
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("8.8.8.8")
        });

        var result = await validator.ValidateAsync("https://api.example.com");

        Assert.True(result.IsAllowed);
    }
}

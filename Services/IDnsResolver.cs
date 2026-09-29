using System.Net;

namespace AITestHub.Services;

/// <summary>
/// Abstraction over DNS resolution so that endpoint security validation
/// can be unit tested without real network access.
/// </summary>
public interface IDnsResolver
{
    Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken = default);
}

public sealed class DnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken = default) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

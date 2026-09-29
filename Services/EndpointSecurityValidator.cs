using System.Net;
using System.Net.Sockets;

namespace AITestHub.Services;

/// <summary>
/// SSRF protection for user-provided endpoints.
///
/// Blocks localhost, loopback, private, link-local and unique-local IPv4/IPv6
/// targets (including cloud metadata endpoints such as 169.254.169.254),
/// non-http(s) schemes, and hosts whose DNS records resolve to such addresses.
/// </summary>
public sealed class EndpointSecurityValidator : IEndpointSecurityValidator
{
    private readonly IDnsResolver _dnsResolver;

    public EndpointSecurityValidator(IDnsResolver dnsResolver)
    {
        _dnsResolver = dnsResolver;
    }

    public async Task<EndpointValidationResult> ValidateAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return EndpointValidationResult.Blocked("The endpoint is empty.");
        }

        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host))
        {
            return EndpointValidationResult.Blocked("The endpoint is not a valid absolute URL.");
        }

        if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointValidationResult.Blocked("Only http and https endpoints are allowed.");
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.IdnHost, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointValidationResult.Blocked("Localhost endpoints are not allowed.");
        }

        IPAddress[] addresses;

        if (IPAddress.TryParse(uri.Host, out var literalAddress))
        {
            addresses = new[] { literalAddress };
        }
        else
        {
            try
            {
                addresses = await _dnsResolver.GetHostAddressesAsync(uri.IdnHost, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Do not leak resolver error details.
                return EndpointValidationResult.Blocked("The endpoint host could not be resolved.");
            }

            if (addresses.Length == 0)
            {
                return EndpointValidationResult.Blocked("The endpoint host could not be resolved.");
            }
        }

        foreach (var address in addresses)
        {
            if (IsPrivateOrLocalAddress(address))
            {
                return EndpointValidationResult.Blocked(
                    "The endpoint host resolves to a private, loopback, link-local or otherwise blocked address.");
            }
        }

        return EndpointValidationResult.Allowed();
    }

    /// <summary>
    /// Checks whether an IP address is loopback, private, link-local, unique-local,
    /// unspecified, multicast or an IPv4-mapped variant of one of those.
    /// </summary>
    internal static bool IsPrivateOrLocalAddress(IPAddress address)
    {
        // Normalize IPv4-mapped IPv6 addresses (e.g. ::ffff:127.0.0.1) to IPv4
        // so the mapped form cannot bypass the IPv4 checks.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            // 0.0.0.0/8 unspecified
            if (bytes[0] == 0)
            {
                return true;
            }

            // 127.0.0.0/8 loopback
            if (bytes[0] == 127)
            {
                return true;
            }

            // 10.0.0.0/8 private
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12 private
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16 private
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 169.254.0.0/16 link-local (incl. cloud metadata 169.254.169.254)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal      // fe80::/10
                || address.IsIPv6SiteLocal   // fec0::/10
                || address.IsIPv6Multicast   // ff00::/8
                || IPAddress.IsLoopback(address) // ::1
                || address.Equals(IPAddress.IPv6Any)
                || address.Equals(IPAddress.IPv6None))
            {
                return true;
            }

            var bytes = address.GetAddressBytes();

            // fc00::/7 unique-local (fc/fd prefix)
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            // IPv4-compatible (deprecated ::a.b.c.d form)
            if (bytes.AsSpan(0, 12).ToArray().All(b => b == 0) && bytes[12] != 0)
            {
                return true;
            }

            return false;
        }

        // Unknown address families are treated as blocked (fail closed).
        return true;
    }
}

namespace AITestHub.Services;

/// <summary>
/// Result of a URL normalization attempt.
/// </summary>
/// <param name="Success">True when normalization succeeded.</param>
/// <param name="Url">The normalized absolute URL on success.</param>
/// <param name="Error">Safe error message on failure.</param>
public sealed record UrlNormalizationResult(bool Success, string? Url = null, string? Error = null)
{
    public static UrlNormalizationResult Ok(string url) => new(true, url);

    public static UrlNormalizationResult Fail(string error) => new(false, Error: error);
}

/// <summary>
/// Deterministic BaseUrl + Endpoint combiner. Never relies on the LLM for URL
/// construction. Rules:
/// - BaseUrl must be an absolute http/https URL.
/// - A relative endpoint is appended to the base path (leading/trailing slashes handled).
/// - An absolute endpoint must use the same scheme and host (incl. port) as the BaseUrl;
///   anything else (different host, scheme downgrade/upgrade) is rejected.
/// - Query strings are preserved.
/// </summary>
public interface IUrlNormalizer
{
    UrlNormalizationResult Normalize(string baseUrl, string endpoint);
}

public sealed class UrlNormalizer : IUrlNormalizer
{
    public UrlNormalizationResult Normalize(string baseUrl, string endpoint)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            return UrlNormalizationResult.Fail("The base URL is not a valid absolute URL.");
        }

        if (!IsHttpScheme(baseUri.Scheme))
        {
            return UrlNormalizationResult.Fail("The base URL must use the http or https scheme.");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return UrlNormalizationResult.Fail("The endpoint is empty.");
        }

        var trimmedEndpoint = endpoint.Trim();

        // Absolute endpoint: must match the base scheme and host (authority).
        if (Uri.TryCreate(trimmedEndpoint, UriKind.Absolute, out var absoluteEndpoint))
        {
            if (!IsHttpScheme(absoluteEndpoint.Scheme))
            {
                return UrlNormalizationResult.Fail("The endpoint must use the http or https scheme.");
            }

            if (!string.Equals(absoluteEndpoint.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return UrlNormalizationResult.Fail(
                    $"The endpoint scheme ('{absoluteEndpoint.Scheme}') does not match the base URL scheme ('{baseUri.Scheme}').");
            }

            if (!string.Equals(absoluteEndpoint.Authority, baseUri.Authority, StringComparison.OrdinalIgnoreCase))
            {
                return UrlNormalizationResult.Fail(
                    "The endpoint host does not match the base URL host. Cross-host endpoints are not allowed.");
            }

            return UrlNormalizationResult.Ok(absoluteEndpoint.AbsoluteUri);
        }

        // Relative endpoint: combine with the base URL.
        // Ensure the base ends with a slash and the relative part does not start with one,
        // so "https://example.com" + "api/users" and "https://example.com/" + "/api/users"
        // both produce "https://example.com/api/users".
        var baseWithSlash = baseUrl.Trim().TrimEnd('/') + "/";
        var relative = trimmedEndpoint.TrimStart('/');

        if (!Uri.TryCreate(baseWithSlash, UriKind.Absolute, out var baseWithSlashUri))
        {
            return UrlNormalizationResult.Fail("The base URL is not a valid absolute URL.");
        }

        if (!Uri.TryCreate(baseWithSlashUri, relative, out var combined) || !IsHttpScheme(combined.Scheme))
        {
            return UrlNormalizationResult.Fail("The endpoint could not be combined with the base URL.");
        }

        return UrlNormalizationResult.Ok(combined.AbsoluteUri);
    }

    private static bool IsHttpScheme(string scheme) =>
        string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase)
        || string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
}

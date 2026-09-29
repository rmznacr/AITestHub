namespace AITestHub.Services;

/// <summary>
/// Result of an endpoint security (SSRF) validation.
/// </summary>
/// <param name="IsAllowed">True when the endpoint may be requested.</param>
/// <param name="Reason">Safe, user-facing reason when the endpoint is blocked.</param>
public sealed record EndpointValidationResult(bool IsAllowed, string? Reason = null)
{
    public static EndpointValidationResult Allowed() => new(true);

    public static EndpointValidationResult Blocked(string reason) => new(false, reason);
}

/// <summary>
/// Validates user-provided endpoints before the backend sends HTTP requests to them
/// (basic SSRF protection).
/// </summary>
public interface IEndpointSecurityValidator
{
    Task<EndpointValidationResult> ValidateAsync(string endpoint, CancellationToken cancellationToken = default);
}

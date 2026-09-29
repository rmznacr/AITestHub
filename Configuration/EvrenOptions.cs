namespace AITestHub.Configuration;

/// <summary>
/// EVREN LLM API settings.
/// ApiKey is intentionally NOT stored in appsettings.json;
/// it is provided via the 'Evren__ApiKey' environment variable (or user-secrets).
/// </summary>
public class EvrenOptions
{
    public const string SectionName = "Evren";

    public string BaseUrl { get; set; } = "https://evren-llmapi.ssyz.org.tr/v1";

    public string Model { get; set; } = "glm-5.3";

    public string ApiKey { get; set; } = string.Empty;
}

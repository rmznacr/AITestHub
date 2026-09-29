using System.Text.Json;
using System.Text.Json.Serialization;
using AITestHub.Configuration;
using Microsoft.Extensions.Options;

namespace AITestHub.Services;

/// <summary>
/// Shared, low-level client for the EVREN chat completions API
/// (OpenAI-compatible endpoint, GLM model). Used by all EVREN-based
/// generator services so that HTTP, authentication and error handling
/// logic is implemented exactly once.
///
/// The API key is sent via the X-API-Key header and is never logged
/// or included in error messages.
/// </summary>
public class EvrenChatClient
{
    private static readonly JsonSerializerOptions ChatJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly EvrenOptions _options;

    public EvrenChatClient(HttpClient httpClient, IOptions<EvrenOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <summary>
    /// Sends a chat completion request and returns the assistant message content.
    /// Throws <see cref="EvrenConfigurationException"/> with a safe message on
    /// configuration, HTTP or parsing errors.
    /// </summary>
    public async Task<string> GetChatContentAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new EvrenConfigurationException(
                "EVREN API key is not configured. Set the 'Evren__ApiKey' environment variable and restart the application.");
        }

        var payload = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-API-Key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpError(response);
        }

        var body = await response.Content.ReadFromJsonAsync<EvrenChatResponse>(ChatJsonOptions, cancellationToken);

        var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new EvrenConfigurationException("The EVREN API returned an empty response content.");
        }

        return content;
    }

    /// <summary>
    /// Locates the outermost JSON array in an LLM response. Tolerates markdown
    /// code fences and surrounding explanatory text.
    /// </summary>
    internal static string ExtractJsonArray(string content)
    {
        var trimmed = content.Trim();

        // Strip markdown code fences (```json ... ``` or ``` ... ```).
        if (trimmed.StartsWith("```"))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd > 0)
            {
                trimmed = trimmed[(firstLineEnd + 1)..];
            }

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }

            trimmed = trimmed.Trim();
        }

        // Locate the outermost JSON array in case the model added extra text.
        var start = trimmed.IndexOf('[');
        var end = trimmed.LastIndexOf(']');

        if (start < 0 || end <= start)
        {
            throw new EvrenConfigurationException(
                "The EVREN API response does not contain a JSON array of test cases.");
        }

        return trimmed[start..(end + 1)];
    }

    /// <summary>
    /// Creates a safe, meaningful error for a non-success EVREN API response.
    /// Messages never contain the API key, request headers or the raw response body.
    /// </summary>
    private static EvrenConfigurationException CreateHttpError(HttpResponseMessage response)
    {
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "The EVREN API rejected the request: authentication failed (401). Verify that the configured API key is valid.",
            System.Net.HttpStatusCode.Forbidden =>
                "The EVREN API rejected the request: access is forbidden (403). The configured API key may not have permission to use this resource.",
            System.Net.HttpStatusCode.TooManyRequests =>
                "The EVREN API rate limit was exceeded (429). Too many requests were sent; wait a while and try again.",
            >= System.Net.HttpStatusCode.InternalServerError =>
                $"The EVREN API returned a server error ({(int)response.StatusCode}). The service may be temporarily unavailable; try again later.",
            _ =>
                $"The EVREN API request failed with status code {(int)response.StatusCode} ({response.StatusCode})."
        };

        return new EvrenConfigurationException(message);
    }

    private sealed class EvrenChatResponse
    {
        [JsonPropertyName("choices")]
        public List<EvrenChoice>? Choices { get; set; }
    }

    private sealed class EvrenChoice
    {
        [JsonPropertyName("message")]
        public EvrenMessage? Message { get; set; }
    }

    private sealed class EvrenMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}

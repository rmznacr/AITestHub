using AITestHub.Entities;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AITestHub.Services;

/// <summary>
/// Default <see cref="IApiTestExecutorService"/> implementation using a typed
/// HttpClient (30 s timeout, automatic redirects disabled) and endpoint
/// security validation.
/// </summary>
public class ApiTestExecutorService : IApiTestExecutorService
{
    /// <summary>
    /// Maximum length of the stored response body (~100 KB). Longer bodies are
    /// truncated; truncation never affects the status code assertion.
    /// </summary>
    public const int MaxResponseBodyLength = 100 * 1024;

    private const string TruncationSuffix = "\n[... response body truncated ...]";

    private static readonly HashSet<string> SupportedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(HttpMethod.Get), nameof(HttpMethod.Post), nameof(HttpMethod.Put), nameof(HttpMethod.Patch), nameof(HttpMethod.Delete)
    };

    private readonly HttpClient _httpClient;
    private readonly IEndpointSecurityValidator _endpointSecurityValidator;
    private readonly ILogger<ApiTestExecutorService> _logger;

    public ApiTestExecutorService(
        HttpClient httpClient,
        IEndpointSecurityValidator endpointSecurityValidator,
        ILogger<ApiTestExecutorService> logger)
    {
        _httpClient = httpClient;
        _endpointSecurityValidator = endpointSecurityValidator;
        _logger = logger;
    }

    public async Task<ApiTestExecutionResult> ExecuteAsync(TestCase testCase, CancellationToken cancellationToken = default)
    {
        var executedAt = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var method = testCase.HttpMethod?.Trim() ?? string.Empty;

        if (!SupportedMethods.Contains(method))
        {
            return Fail(executedAt, stopwatch, method, testCase,
                $"The HTTP method '{method}' is not supported. Supported methods: GET, POST, PUT, PATCH, DELETE.");
        }

        if (testCase.ExpectedStatusCode is < 100 or > 599)
        {
            return Fail(executedAt, stopwatch, method, testCase,
                $"The expected status code {testCase.ExpectedStatusCode} is not a valid HTTP status code.");
        }

        var security = await _endpointSecurityValidator.ValidateAsync(testCase.Endpoint ?? string.Empty, cancellationToken);
        if (!security.IsAllowed)
        {
            return Fail(executedAt, stopwatch, method, testCase, security.Reason ?? "The endpoint was blocked by security validation.");
        }

        string? requestBody = null;

        // GET and DELETE do not carry a request body.
        var carriesBody = !string.Equals(method, nameof(HttpMethod.Get), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(method, nameof(HttpMethod.Delete), StringComparison.OrdinalIgnoreCase);

        if (carriesBody && !string.IsNullOrWhiteSpace(testCase.RequestBody))
        {
            if (!IsValidJson(testCase.RequestBody))
            {
                return Fail(executedAt, stopwatch, method, testCase, "The request body is not valid JSON.");
            }

            requestBody = testCase.RequestBody;
        }

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), testCase.Endpoint);

            if (requestBody is not null)
            {
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var responseBody = await ReadResponseBodySafelyAsync(response, cancellationToken);
            var actualStatusCode = (int)response.StatusCode;

            stopwatch.Stop();

            var passed = actualStatusCode == testCase.ExpectedStatusCode;

            return new ApiTestExecutionResult
            {
                Passed = passed,
                ExecutedAt = executedAt,
                DurationMs = stopwatch.ElapsedMilliseconds,
                RequestMethod = method.ToUpperInvariant(),
                RequestUrl = testCase.Endpoint ?? string.Empty,
                RequestBody = requestBody,
                ActualStatusCode = actualStatusCode,
                ResponseBody = responseBody,
                ErrorMessage = passed
                    ? null
                    : $"Expected HTTP {testCase.ExpectedStatusCode}, received {actualStatusCode}."
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-initiated cancellation: propagate so the caller can handle it.
            throw;
        }
        catch (OperationCanceledException)
        {
            // HttpClient timeout surfaces as TaskCanceledException without caller cancellation.
            return Fail(executedAt, stopwatch, method, testCase, "The request timed out.");
        }
        catch (HttpRequestException)
        {
            return Fail(executedAt, stopwatch, method, testCase,
                "The request could not be completed (DNS failure, connection refused or similar network error).");
        }
        catch (Exception)
        {
            // Never leak stack traces or raw exception details.
            _logger.LogWarning("Automated execution of test case {TestCaseId} failed with an unexpected error.", testCase.Id);
            return Fail(executedAt, stopwatch, method, testCase, "The request failed with an unexpected error.");
        }
    }

    private static ApiTestExecutionResult Fail(
        DateTime executedAt,
        Stopwatch stopwatch,
        string method,
        TestCase testCase,
        string errorMessage)
    {
        stopwatch.Stop();

        return new ApiTestExecutionResult
        {
            Passed = false,
            ExecutedAt = executedAt,
            DurationMs = stopwatch.ElapsedMilliseconds,
            RequestMethod = string.IsNullOrEmpty(method) ? "UNKNOWN" : method.ToUpperInvariant(),
            RequestUrl = testCase.Endpoint ?? string.Empty,
            RequestBody = testCase.RequestBody,
            ActualStatusCode = 0,
            ResponseBody = null,
            ErrorMessage = errorMessage
        };
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<string?> ReadResponseBodySafelyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
        {
            return null;
        }

        string body;

        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Binary or otherwise unreadable content.
            return "[unreadable response body]";
        }

        if (body.Length > MaxResponseBodyLength)
        {
            body = body.Substring(0, MaxResponseBodyLength) + TruncationSuffix;
        }

        return body;
    }
}

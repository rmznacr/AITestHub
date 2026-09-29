using AITestHub.Entities;

namespace AITestHub.Services;

/// <summary>
/// Raw outcome of a single HTTP execution performed by the API test executor.
/// This is an intermediate model; persistence and DTO mapping are handled by
/// <see cref="ITestExecutionService"/>.
/// </summary>
public class ApiTestExecutionResult
{
    public bool Passed { get; set; }

    public DateTime ExecutedAt { get; set; }

    public long DurationMs { get; set; }

    public string RequestMethod { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public int ActualStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Executes test cases by sending real HTTP requests and evaluating the
/// status code assertion. Never throws for test-level failures (network
/// errors, timeouts, blocked endpoints, unsupported methods, malformed
/// bodies); instead it returns a failed result with a safe error message.
/// Caller-initiated cancellation is propagated.
/// </summary>
public interface IApiTestExecutorService
{
    Task<ApiTestExecutionResult> ExecuteAsync(TestCase testCase, CancellationToken cancellationToken = default);
}

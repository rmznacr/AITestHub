namespace AITestHub.Entities;

/// <summary>
/// Execution history entry produced by the automated API test executor.
/// A test case can be executed multiple times; every run is recorded here.
/// </summary>
public class TestExecutionResult
{
    public int Id { get; set; }

    public int TestCaseId { get; set; }

    public TestCase TestCase { get; set; } = null!;

    /// <summary>
    /// UTC timestamp of the execution.
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Duration of the HTTP request in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    public string RequestMethod { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public int ActualStatusCode { get; set; }

    public int ExpectedStatusCode { get; set; }

    /// <summary>
    /// Truncated response body (max ~100 KB is stored).
    /// </summary>
    public string? ResponseBody { get; set; }

    public bool Passed { get; set; }

    public string? ErrorMessage { get; set; }
}

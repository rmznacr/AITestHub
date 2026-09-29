using AITestHub.Entities;

namespace AITestHub.DTOs;

/// <summary>
/// Detailed view of a single execution history entry.
/// </summary>
public class ExecutionDetailDto
{
    public int Id { get; set; }

    public int TestCaseId { get; set; }

    public DateTime ExecutedAt { get; set; }

    public long DurationMs { get; set; }

    public string RequestMethod { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public int ExpectedStatusCode { get; set; }

    public int ActualStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public bool Passed { get; set; }

    public string? ErrorMessage { get; set; }

    public static ExecutionDetailDto FromEntity(TestExecutionResult result) => new()
    {
        Id = result.Id,
        TestCaseId = result.TestCaseId,
        ExecutedAt = result.ExecutedAt,
        DurationMs = result.DurationMs,
        RequestMethod = result.RequestMethod,
        RequestUrl = result.RequestUrl,
        RequestBody = result.RequestBody,
        ExpectedStatusCode = result.ExpectedStatusCode,
        ActualStatusCode = result.ActualStatusCode,
        ResponseBody = result.ResponseBody,
        Passed = result.Passed,
        ErrorMessage = result.ErrorMessage
    };
}

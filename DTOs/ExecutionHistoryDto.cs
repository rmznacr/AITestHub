using AITestHub.Entities;

namespace AITestHub.DTOs;

/// <summary>
/// Lightweight execution history entry (no response/request bodies).
/// </summary>
public class ExecutionHistoryDto
{
    public int Id { get; set; }

    public int TestCaseId { get; set; }

    public DateTime ExecutedAt { get; set; }

    public long DurationMs { get; set; }

    public string RequestMethod { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    public int ExpectedStatusCode { get; set; }

    public int ActualStatusCode { get; set; }

    public bool Passed { get; set; }

    public string? ErrorMessage { get; set; }

    public static ExecutionHistoryDto FromEntity(TestExecutionResult result) => new()
    {
        Id = result.Id,
        TestCaseId = result.TestCaseId,
        ExecutedAt = result.ExecutedAt,
        DurationMs = result.DurationMs,
        RequestMethod = result.RequestMethod,
        RequestUrl = result.RequestUrl,
        ExpectedStatusCode = result.ExpectedStatusCode,
        ActualStatusCode = result.ActualStatusCode,
        Passed = result.Passed,
        ErrorMessage = result.ErrorMessage
    };
}

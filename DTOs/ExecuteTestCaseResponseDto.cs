using AITestHub.Entities;
using AITestHub.Entities.Enums;

namespace AITestHub.DTOs;

/// <summary>
/// Result of a single automated test execution (persisted history entry included).
/// </summary>
public class ExecuteTestCaseResponseDto
{
    public int TestCaseId { get; set; }

    public int ExecutionResultId { get; set; }

    /// <summary>
    /// Updated status of the test case after the execution (Passed/Failed).
    /// </summary>
    public TestStatus Status { get; set; }

    public bool Passed { get; set; }

    public DateTime ExecutedAt { get; set; }

    public long DurationMs { get; set; }

    public string RequestMethod { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    public int ExpectedStatusCode { get; set; }

    public int ActualStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }

    public static ExecuteTestCaseResponseDto FromEntity(TestExecutionResult result, TestStatus status) => new()
    {
        TestCaseId = result.TestCaseId,
        ExecutionResultId = result.Id,
        Status = status,
        Passed = result.Passed,
        ExecutedAt = result.ExecutedAt,
        DurationMs = result.DurationMs,
        RequestMethod = result.RequestMethod,
        RequestUrl = result.RequestUrl,
        ExpectedStatusCode = result.ExpectedStatusCode,
        ActualStatusCode = result.ActualStatusCode,
        ResponseBody = result.ResponseBody,
        ErrorMessage = result.ErrorMessage
    };
}

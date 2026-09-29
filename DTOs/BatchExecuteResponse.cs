namespace AITestHub.DTOs;

/// <summary>
/// Aggregated result of a batch execution.
/// </summary>
public class BatchExecuteResponse
{
    /// <summary>
    /// Number of test cases actually executed (missing ids are skipped).
    /// </summary>
    public int Total { get; set; }

    public int Passed { get; set; }

    public int Failed { get; set; }

    public List<ExecuteTestCaseResponseDto> Results { get; set; } = new();
}

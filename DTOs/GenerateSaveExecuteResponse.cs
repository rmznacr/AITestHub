namespace AITestHub.DTOs;

/// <summary>
/// Result of a single test case within the generate-save-execute orchestration.
/// </summary>
public class GenerateSaveExecuteResultDto
{
    public int TestCaseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public int ExpectedStatusCode { get; set; }

    public int ActualStatusCode { get; set; }

    public bool Passed { get; set; }

    public long DurationMs { get; set; }

    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Aggregate response of the generate-save-execute orchestration endpoint.
/// </summary>
public class GenerateSaveExecuteResponse
{
    public int ProjectId { get; set; }

    public int GeneratedCount { get; set; }

    public int SavedCount { get; set; }

    public int ExecutedCount { get; set; }

    public int Passed { get; set; }

    public int Failed { get; set; }

    public List<GenerateSaveExecuteResultDto> Results { get; set; } = new();
}

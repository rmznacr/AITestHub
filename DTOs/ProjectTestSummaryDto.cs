namespace AITestHub.DTOs;

/// <summary>
/// Test execution summary of a project.
/// </summary>
public class ProjectTestSummaryDto
{
    public int ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public int TotalTests { get; set; }

    public int NotRun { get; set; }

    public int Passed { get; set; }

    public int Failed { get; set; }

    public int Blocked { get; set; }

    /// <summary>
    /// Passed / (Passed + Failed + Blocked) * 100. Zero when nothing has been executed.
    /// </summary>
    public double PassRate { get; set; }
}

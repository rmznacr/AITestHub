namespace AITestHub.DTOs;

/// <summary>
/// Global test execution summary shown on the dashboard.
/// </summary>
public class DashboardSummaryDto
{
    public int TotalProjects { get; set; }

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

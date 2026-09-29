using AITestHub.Data;
using AITestHub.DTOs;
using AITestHub.Entities.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AITestHub.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the global test execution summary for the dashboard.
    /// All counts are aggregated on the database side.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var totalProjects = await _context.Projects
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var counts = await _context.TestCases
            .AsNoTracking()
            .GroupBy(t => 1)
            .Select(g => new
            {
                TotalTests = g.Count(),
                NotRun = g.Count(t => t.Status == TestStatus.NotRun),
                Passed = g.Count(t => t.Status == TestStatus.Passed),
                Failed = g.Count(t => t.Status == TestStatus.Failed),
                Blocked = g.Count(t => t.Status == TestStatus.Blocked)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var passed = counts?.Passed ?? 0;
        var failed = counts?.Failed ?? 0;
        var blocked = counts?.Blocked ?? 0;
        var executed = passed + failed + blocked;

        return Ok(new DashboardSummaryDto
        {
            TotalProjects = totalProjects,
            TotalTests = counts?.TotalTests ?? 0,
            NotRun = counts?.NotRun ?? 0,
            Passed = passed,
            Failed = failed,
            Blocked = blocked,
            PassRate = executed == 0 ? 0 : Math.Round(passed * 100.0 / executed, 2)
        });
    }
}

using AITestHub.Data;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Entities.Enums;
using AITestHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AITestHub.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProjectsController> _logger;
    private readonly ITestExecutionService _executionService;

    public ProjectsController(
        AppDbContext context,
        ILogger<ProjectsController> logger,
        ITestExecutionService executionService)
    {
        _context = context;
        _logger = logger;
        _executionService = executionService;
    }

    /// <summary>
    /// Returns all projects.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Project>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Project>>> GetProjects(CancellationToken cancellationToken)
    {
        return Ok(await _context.Projects.AsNoTracking().ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Returns a single project by id, including its test cases.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Project), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Project>> GetProject(int id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects.AsNoTracking()
            .Include(p => p.TestCases)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        return Ok(project);
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Project), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Project>> CreateProject(Project project, CancellationToken cancellationToken)
    {
        project.Id = 0;
        project.CreatedAt = DateTime.UtcNow;

        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
    }

    /// <summary>
    /// Updates an existing project.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProject(int id, Project project, CancellationToken cancellationToken)
    {
        if (id != project.Id)
        {
            return BadRequest();
        }

        var existing = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = project.Name;
        existing.Description = project.Description;

        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes a project by id. Test cases belonging to the project are deleted as well (cascade).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(int id, CancellationToken cancellationToken)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Returns the test execution summary of a project.
    /// Counts are aggregated on the database side; no test case rows are loaded into memory.
    /// </summary>
    [HttpGet("{projectId:int}/test-summary")]
    [ProducesResponseType(typeof(ProjectTestSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectTestSummaryDto>> GetProjectTestSummary(
        int projectId,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return NotFound($"Project with id {projectId} was not found.");
        }

        var counts = await _context.TestCases
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .GroupBy(t => t.ProjectId)
            .Select(g => new
            {
                TotalTests = g.Count(),
                NotRun = g.Count(t => t.Status == TestStatus.NotRun),
                Passed = g.Count(t => t.Status == TestStatus.Passed),
                Failed = g.Count(t => t.Status == TestStatus.Failed),
                Blocked = g.Count(t => t.Status == TestStatus.Blocked)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalTests = counts?.TotalTests ?? 0;
        var passed = counts?.Passed ?? 0;
        var failed = counts?.Failed ?? 0;
        var blocked = counts?.Blocked ?? 0;

        var executed = passed + failed + blocked;

        return Ok(new ProjectTestSummaryDto
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            TotalTests = totalTests,
            NotRun = counts?.NotRun ?? 0,
            Passed = passed,
            Failed = failed,
            Blocked = blocked,
            PassRate = executed == 0 ? 0 : Math.Round(passed * 100.0 / executed, 2)
        });
    }

    /// <summary>
    /// Executes all executable test cases of the project (max 50 per request,
    /// ordered by id for deterministic behavior). A test case is executable when
    /// it has an HTTP method, an endpoint and a valid expected status code.
    /// </summary>
    [HttpPost("{projectId:int}/execute")]
    [ProducesResponseType(typeof(BatchExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchExecuteResponse>> ExecuteProjectTests(
        int projectId,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);

        if (project is null)
        {
            return NotFound($"Project with id {projectId} was not found.");
        }

        var executableTestCases = await _context.TestCases
            .Where(t => t.ProjectId == projectId
                && t.HttpMethod != ""
                && t.Endpoint != ""
                && t.ExpectedStatusCode >= 100
                && t.ExpectedStatusCode <= 599)
            .OrderBy(t => t.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var response = new BatchExecuteResponse();

        foreach (var testCase in executableTestCases)
        {
            try
            {
                var result = await _executionService.ExecuteAndPersistAsync(testCase, cancellationToken);
                response.Results.Add(result);

                if (result.Passed)
                {
                    response.Passed++;
                }
                else
                {
                    response.Failed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                _logger.LogWarning("Project execution of test case {TestCaseId} failed unexpectedly.", testCase.Id);
            }
        }

        response.Total = response.Results.Count;

        return Ok(response);
    }
}

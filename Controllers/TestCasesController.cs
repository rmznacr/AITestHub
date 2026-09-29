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
public class TestCasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<TestCasesController> _logger;
    private readonly ITestExecutionService _executionService;
    private readonly IEndpointSecurityValidator _endpointSecurityValidator;

    public TestCasesController(
        AppDbContext context,
        ILogger<TestCasesController> logger,
        ITestExecutionService executionService,
        IEndpointSecurityValidator endpointSecurityValidator)
    {
        _context = context;
        _logger = logger;
        _executionService = executionService;
        _endpointSecurityValidator = endpointSecurityValidator;
    }

    /// <summary>
    /// Returns all test cases. Optionally filtered by project id.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TestCaseResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TestCaseResponseDto>>> GetTestCases(
        [FromQuery] int? projectId,
        CancellationToken cancellationToken)
    {
        var query = _context.TestCases.AsNoTracking();

        if (projectId.HasValue)
        {
            query = query.Where(t => t.ProjectId == projectId.Value);
        }

        var testCases = await query.ToListAsync(cancellationToken);
        return Ok(testCases.Select(TestCaseResponseDto.FromEntity));
    }

    /// <summary>
    /// Returns a single test case by id.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TestCaseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCaseResponseDto>> GetTestCase(int id, CancellationToken cancellationToken)
    {
        var testCase = await _context.TestCases.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (testCase is null)
        {
            return NotFound();
        }

        return Ok(TestCaseResponseDto.FromEntity(testCase));
    }

    /// <summary>
    /// Creates a new test case.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TestCase), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCase>> CreateTestCase(TestCase testCase, CancellationToken cancellationToken)
    {
        testCase.Id = 0;
        testCase.CreatedAt = DateTime.UtcNow;
        testCase.Project = null!;

        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == testCase.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return NotFound($"Project with id {testCase.ProjectId} was not found.");
        }

        _context.TestCases.Add(testCase);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetTestCase), new { id = testCase.Id }, TestCaseResponseDto.FromEntity(testCase));
    }

    /// <summary>
    /// Updates an existing test case.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTestCase(int id, TestCase testCase, CancellationToken cancellationToken)
    {
        if (id != testCase.Id)
        {
            return BadRequest();
        }

        var existing = await _context.TestCases
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (existing is null)
        {
            return NotFound();
        }

        if (!await _context.Projects.AnyAsync(p => p.Id == testCase.ProjectId, cancellationToken))
        {
            return NotFound($"Project with id {testCase.ProjectId} was not found.");
        }

        existing.Name = testCase.Name;
        existing.Description = testCase.Description;
        existing.HttpMethod = testCase.HttpMethod;
        existing.Endpoint = testCase.Endpoint;
        existing.RequestBody = testCase.RequestBody;
        existing.ExpectedStatusCode = testCase.ExpectedStatusCode;
        existing.ProjectId = testCase.ProjectId;
        existing.Preconditions = testCase.Preconditions;
        existing.Steps = testCase.Steps;
        existing.ExpectedResult = testCase.ExpectedResult;
        existing.Priority = testCase.Priority;
        existing.TestType = testCase.TestType;
        // Execution-related fields (Status, ExecutionNotes, LastExecutedAt) are intentionally
        // NOT updated here; they must be managed through the execution and reset endpoints.

        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes a test case by id.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTestCase(int id, CancellationToken cancellationToken)
    {
        var testCase = await _context.TestCases
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (testCase is null)
        {
            return NotFound();
        }

        _context.TestCases.Remove(testCase);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Records a test execution result for a test case.
    /// Only Passed, Failed and Blocked are accepted as execution results.
    /// </summary>
    [HttpPatch("{id:int}/execution")]
    [ProducesResponseType(typeof(TestCaseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCaseResponseDto>> UpdateTestExecution(
        int id,
        UpdateTestExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Status is not (TestStatus.Passed or TestStatus.Failed or TestStatus.Blocked))
        {
            return BadRequest(new { error = "Status must be one of: Passed, Failed, Blocked. Use the reset endpoint to set NotRun." });
        }

        var testCase = await _context.TestCases
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (testCase is null)
        {
            return NotFound();
        }

        testCase.Status = request.Status.Value;
        testCase.ExecutionNotes = request.ExecutionNotes;
        testCase.LastExecutedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(TestCaseResponseDto.FromEntity(testCase));
    }

    /// <summary>
    /// Resets the execution state of a test case back to NotRun.
    /// </summary>
    [HttpPatch("{id:int}/reset")]
    [ProducesResponseType(typeof(TestCaseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCaseResponseDto>> ResetTestExecution(
        int id,
        CancellationToken cancellationToken)
    {
        var testCase = await _context.TestCases
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (testCase is null)
        {
            return NotFound();
        }

        testCase.Status = TestStatus.NotRun;
        testCase.ExecutionNotes = null;
        testCase.LastExecutedAt = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(TestCaseResponseDto.FromEntity(testCase));
    }

    /// <summary>
    /// Executes a single test case by sending a real HTTP request to its endpoint
    /// and records the result. A failed assertion still returns 200 OK with
    /// Passed = false in the body.
    /// </summary>
    [HttpPost("{id:int}/execute")]
    [ProducesResponseType(typeof(ExecuteTestCaseResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecuteTestCaseResponseDto>> ExecuteTestCase(
        int id,
        CancellationToken cancellationToken)
    {
        var testCase = await _context.TestCases
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (testCase is null)
        {
            return NotFound();
        }

        var validationError = await ValidateExecutableTestCaseAsync(testCase, cancellationToken);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        return Ok(await _executionService.ExecuteAndPersistAsync(testCase, cancellationToken));
    }

    /// <summary>
    /// Executes a batch of test cases sequentially. Duplicate ids are de-duplicated;
    /// missing ids are skipped; a failure of one test case never stops the others.
    /// </summary>
    [HttpPost("execute-batch")]
    [ProducesResponseType(typeof(BatchExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchExecuteResponse>> ExecuteTestCasesBatch(
        BatchExecuteRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.TestCaseIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return BadRequest(new { error = "At least one test case id is required." });
        }

        if (ids.Count > 50)
        {
            return BadRequest(new { error = "A maximum of 50 test cases can be executed per batch." });
        }

        var testCases = await _context.TestCases
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var response = new BatchExecuteResponse();

        // Preserve the requested order; skip ids that do not exist.
        foreach (var id in ids)
        {
            if (!testCases.TryGetValue(id, out var testCase))
            {
                continue;
            }

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
                _logger.LogWarning("Batch execution of test case {TestCaseId} failed unexpectedly.", id);
            }
        }

        response.Total = response.Results.Count;

        return Ok(response);
    }

    /// <summary>
    /// Returns the automated execution history of a test case, newest first.
    /// </summary>
    [HttpGet("{id:int}/executions")]
    [ProducesResponseType(typeof(IEnumerable<ExecutionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ExecutionHistoryDto>>> GetExecutionHistory(
        int id,
        CancellationToken cancellationToken)
    {
        var exists = await _context.TestCases
            .AnyAsync(t => t.Id == id, cancellationToken);

        if (!exists)
        {
            return NotFound();
        }

        return Ok(await _executionService.GetHistoryAsync(id, cancellationToken));
    }

    /// <summary>
    /// Validates that a test case is executable. Returns a safe error message or null.
    /// </summary>
    private async Task<string?> ValidateExecutableTestCaseAsync(TestCase testCase, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(testCase.HttpMethod))
        {
            return "The test case has no HTTP method configured.";
        }

        if (string.IsNullOrWhiteSpace(testCase.Endpoint))
        {
            return "The test case has no endpoint configured.";
        }

        if (testCase.ExpectedStatusCode is < 100 or > 599)
        {
            return $"The expected status code {testCase.ExpectedStatusCode} is not a valid HTTP status code.";
        }

        var security = await _endpointSecurityValidator.ValidateAsync(testCase.Endpoint, cancellationToken);
        if (!security.IsAllowed)
        {
            return security.Reason ?? "The endpoint was blocked by security validation.";
        }

        return null;
    }
}

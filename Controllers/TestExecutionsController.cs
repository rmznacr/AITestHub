using AITestHub.DTOs;
using AITestHub.Services;
using Microsoft.AspNetCore.Mvc;

namespace AITestHub.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TestExecutionsController : ControllerBase
{
    private readonly ITestExecutionService _executionService;

    public TestExecutionsController(ITestExecutionService executionService)
    {
        _executionService = executionService;
    }

    /// <summary>
    /// Returns the detailed result of a single automated execution.
    /// </summary>
    [HttpGet("{executionId:int}")]
    [ProducesResponseType(typeof(ExecutionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExecutionDetailDto>> GetExecution(
        int executionId,
        CancellationToken cancellationToken)
    {
        var detail = await _executionService.GetExecutionDetailAsync(executionId, cancellationToken);

        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }
}

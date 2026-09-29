using AITestHub.Data;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AITestHub.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AITestCasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITestCaseGeneratorService _generatorService;
    private readonly IApiTestCaseGeneratorService _apiGeneratorService;
    private readonly IUrlNormalizer _urlNormalizer;
    private readonly IEndpointSecurityValidator _endpointSecurityValidator;
    private readonly ITestExecutionService _executionService;
    private readonly ILogger<AITestCasesController> _logger;

    public AITestCasesController(
        AppDbContext context,
        ITestCaseGeneratorService generatorService,
        IApiTestCaseGeneratorService apiGeneratorService,
        IUrlNormalizer urlNormalizer,
        IEndpointSecurityValidator endpointSecurityValidator,
        ITestExecutionService executionService,
        ILogger<AITestCasesController> logger)
    {
        _context = context;
        _generatorService = generatorService;
        _apiGeneratorService = apiGeneratorService;
        _urlNormalizer = urlNormalizer;
        _endpointSecurityValidator = endpointSecurityValidator;
        _executionService = executionService;
        _logger = logger;
    }

    /// <summary>
    /// Generates test cases for a project requirement using the AI generator.
    /// The generated test cases are NOT persisted; they are only returned in the response.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateTestCasesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateTestCasesResponse>> GenerateTestCases(
        GenerateTestCasesRequest request,
        CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return NotFound($"Project with id {request.ProjectId} was not found.");
        }

        List<DTOs.GeneratedTestCaseDto> generated;
        try
        {
            generated = await _generatorService.GenerateAsync(
                request.Requirement,
                request.Count,
                cancellationToken);
        }
        catch (EvrenConfigurationException ex)
        {
            _logger.LogError("Test case generation failed: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }

        var response = new GenerateTestCasesResponse
        {
            ProjectId = request.ProjectId,
            Requirement = request.Requirement,
            TestCases = generated
        };

        return Ok(response);
    }

    /// <summary>
    /// Saves user-approved AI-generated test cases to the database.
    /// The save is atomic: all test cases are inserted in a single SaveChangesAsync call.
    /// </summary>
    [HttpPost("save")]
    [ProducesResponseType(typeof(SaveGeneratedTestCasesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaveGeneratedTestCasesResponse>> SaveGeneratedTestCases(
        SaveGeneratedTestCasesRequest request,
        CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return NotFound($"Project with id {request.ProjectId} was not found.");
        }

        // DTO -> Entity mapping:
        // Title -> Name, Description -> Description, Preconditions -> Preconditions,
        // Steps -> Steps, ExpectedResult -> ExpectedResult, Priority -> Priority, TestType -> TestType.
        // HttpMethod/Endpoint are not produced by the AI generator; they are left empty
        // and can be filled in later via the existing test case update endpoint.
        // Status defaults to NotRun for newly saved test cases.
        var entities = request.TestCases.Select(dto => new TestCase
        {
            Name = dto.Title,
            Description = dto.Description,
            Preconditions = dto.Preconditions,
            Steps = dto.Steps,
            ExpectedResult = dto.ExpectedResult,
            Priority = dto.Priority ?? Entities.Enums.Priority.Medium,
            TestType = dto.TestType ?? Entities.Enums.TestType.Positive,
            Status = Entities.Enums.TestStatus.NotRun,
            ProjectId = request.ProjectId,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.TestCases.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new SaveGeneratedTestCasesResponse
        {
            ProjectId = request.ProjectId,
            SavedCount = entities.Count,
            TestCases = entities.Select(TestCaseResponseDto.FromEntity).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Generates EXECUTABLE API test cases with AI: the result contains the full HTTP
    /// request definition (method, endpoint, request body, expected status code) so the
    /// saved cases can be run by the automated test executor.
    /// Nothing is persisted by this endpoint.
    /// </summary>
    [HttpPost("generate-api")]
    [ProducesResponseType(typeof(GenerateApiTestCasesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GenerateApiTestCasesResponse>> GenerateApiTestCases(
        GenerateApiTestCasesRequest request,
        CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return NotFound($"Project with id {request.ProjectId} was not found.");
        }

        var requestError = await ValidateGenerateApiRequestAsync(request, cancellationToken);
        if (requestError is not null)
        {
            return BadRequest(new { error = requestError });
        }

        List<GeneratedApiTestCaseDto> generated;
        try
        {
            generated = await _apiGeneratorService.GenerateAsync(request, cancellationToken);
        }
        catch (EvrenConfigurationException ex)
        {
            _logger.LogError("API test case generation failed: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }

        return Ok(new GenerateApiTestCasesResponse
        {
            ProjectId = request.ProjectId,
            BaseUrl = request.BaseUrl,
            Requirement = request.Requirement,
            TestCases = generated
        });
    }

    /// <summary>
    /// Saves user-approved, AI-generated executable API test cases. Every test case is
    /// re-validated (endpoint security, body rules, status code range) before any database
    /// work; the save is atomic (single SaveChangesAsync call).
    /// </summary>
    [HttpPost("save-api")]
    [ProducesResponseType(typeof(SaveGeneratedTestCasesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaveGeneratedTestCasesResponse>> SaveGeneratedApiTestCases(
        SaveGeneratedApiTestCasesRequest request,
        CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return NotFound($"Project with id {request.ProjectId} was not found.");
        }

        // Validate everything BEFORE touching the database: if one case is invalid,
        // none of them are saved.
        foreach (var testCase in request.TestCases)
        {
            var error = await ValidateGeneratedApiTestCaseAsync(testCase, cancellationToken);
            if (error is not null)
            {
                return BadRequest(new { error });
            }
        }

        var entities = request.TestCases.Select(dto => MapToEntity(dto, request.ProjectId)).ToList();

        _context.TestCases.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new SaveGeneratedTestCasesResponse
        {
            ProjectId = request.ProjectId,
            SavedCount = entities.Count,
            TestCases = entities.Select(TestCaseResponseDto.FromEntity).ToList()
        });
    }

    /// <summary>
    /// Convenience orchestration endpoint: generates executable API test cases with AI,
    /// saves them atomically, and then executes each saved test case through the
    /// automated test executor (execution results are persisted individually after the
    /// save has been committed; no database transaction is held open during network calls).
    /// A failing assertion or execution never stops the remaining test cases.
    /// </summary>
    [HttpPost("generate-save-execute")]
    [ProducesResponseType(typeof(GenerateSaveExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GenerateSaveExecuteResponse>> GenerateSaveAndExecuteApiTestCases(
        GenerateApiTestCasesRequest request,
        CancellationToken cancellationToken)
    {
        var projectExists = await _context.Projects
            .AnyAsync(p => p.Id == request.ProjectId, cancellationToken);

        if (!projectExists)
        {
            return NotFound($"Project with id {request.ProjectId} was not found.");
        }

        var requestError = await ValidateGenerateApiRequestAsync(request, cancellationToken);
        if (requestError is not null)
        {
            return BadRequest(new { error = requestError });
        }

        List<GeneratedApiTestCaseDto> generated;
        try
        {
            generated = await _apiGeneratorService.GenerateAsync(request, cancellationToken);
        }
        catch (EvrenConfigurationException ex)
        {
            _logger.LogError("API test case generation failed: {Message}", ex.Message);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }

        // Generate + Save is atomic; the transaction ends with this SaveChangesAsync.
        var entities = generated.Select(dto => MapToEntity(dto, request.ProjectId)).ToList();

        _context.TestCases.AddRange(entities);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new GenerateSaveExecuteResponse
        {
            ProjectId = request.ProjectId,
            GeneratedCount = generated.Count,
            SavedCount = entities.Count
        };

        // Execute individually AFTER the save is committed. One failing test case
        // never stops the others; each execution persists its own history entry.
        foreach (var entity in entities)
        {
            try
            {
                var result = await _executionService.ExecuteAndPersistAsync(entity, cancellationToken);

                response.Results.Add(new GenerateSaveExecuteResultDto
                {
                    TestCaseId = result.TestCaseId,
                    Title = entity.Name,
                    HttpMethod = result.RequestMethod,
                    Endpoint = result.RequestUrl,
                    ExpectedStatusCode = result.ExpectedStatusCode,
                    ActualStatusCode = result.ActualStatusCode,
                    Passed = result.Passed,
                    DurationMs = result.DurationMs,
                    ErrorMessage = result.ErrorMessage
                });

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
                _logger.LogWarning(
                    "Execution of generated test case {TestCaseId} failed unexpectedly.",
                    entity.Id);
            }
        }

        response.ExecutedCount = response.Results.Count;

        return Ok(response);
    }

    /// <summary>
    /// Validates the generate/save-execute request: supported HTTP method and
    /// deterministic BaseUrl + Endpoint normalization with SSRF checks.
    /// Returns a safe error message or null.
    /// </summary>
    private async Task<string?> ValidateGenerateApiRequestAsync(
        GenerateApiTestCasesRequest request,
        CancellationToken cancellationToken)
    {
        if (!EvrenApiTestCaseGeneratorService.SupportedMethods.Contains(request.HttpMethod?.Trim() ?? string.Empty))
        {
            return "HttpMethod must be one of: GET, POST, PUT, PATCH, DELETE.";
        }

        var normalization = _urlNormalizer.Normalize(request.BaseUrl, request.Endpoint);
        if (!normalization.Success || normalization.Url is null)
        {
            return normalization.Error ?? "The base URL or endpoint is invalid.";
        }

        var security = await _endpointSecurityValidator.ValidateAsync(normalization.Url, cancellationToken);
        if (!security.IsAllowed)
        {
            return security.Reason ?? "The endpoint was blocked by security validation.";
        }

        return null;
    }

    /// <summary>
    /// Validates a single generated API test case before saving: supported method,
    /// absolute http/https endpoint, SSRF safety, status code range and body rules.
    /// Returns a safe error message or null.
    /// </summary>
    private async Task<string?> ValidateGeneratedApiTestCaseAsync(
        GeneratedApiTestCaseDto testCase,
        CancellationToken cancellationToken)
    {
        var method = testCase.HttpMethod?.Trim() ?? string.Empty;

        if (!EvrenApiTestCaseGeneratorService.SupportedMethods.Contains(method))
        {
            return "HttpMethod must be one of: GET, POST, PUT, PATCH, DELETE.";
        }

        if (testCase.ExpectedStatusCode is < 100 or > 599)
        {
            return $"ExpectedStatusCode {testCase.ExpectedStatusCode} is not a valid HTTP status code (100-599).";
        }

        if (string.IsNullOrWhiteSpace(testCase.Endpoint)
            || !Uri.TryCreate(testCase.Endpoint.Trim(), UriKind.Absolute, out var endpointUri)
            || (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            return "The endpoint must be an absolute http/https URL.";
        }

        var security = await _endpointSecurityValidator.ValidateAsync(testCase.Endpoint, cancellationToken);
        if (!security.IsAllowed)
        {
            return security.Reason ?? "The endpoint was blocked by security validation.";
        }

        var isBodylessMethod = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);

        if (isBodylessMethod)
        {
            if (!string.IsNullOrWhiteSpace(testCase.RequestBody))
            {
                return $"RequestBody must be null for {method.ToUpperInvariant()} requests.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(testCase.RequestBody) && !IsValidJson(testCase.RequestBody))
        {
            return "RequestBody is not valid JSON.";
        }

        return null;
    }

    private static TestCase MapToEntity(GeneratedApiTestCaseDto dto, int projectId) => new()
    {
        Name = dto.Title,
        Description = dto.Description,
        Preconditions = dto.Preconditions,
        Steps = dto.Steps,
        ExpectedResult = dto.ExpectedResult,
        Priority = dto.Priority ?? Entities.Enums.Priority.Medium,
        TestType = dto.TestType ?? Entities.Enums.TestType.Positive,
        HttpMethod = (dto.HttpMethod ?? string.Empty).Trim().ToUpperInvariant(),
        Endpoint = dto.Endpoint,
        RequestBody = string.IsNullOrWhiteSpace(dto.RequestBody) ? null : dto.RequestBody,
        ExpectedStatusCode = dto.ExpectedStatusCode,
        Status = Entities.Enums.TestStatus.NotRun,
        ProjectId = projectId,
        CreatedAt = DateTime.UtcNow
    };

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

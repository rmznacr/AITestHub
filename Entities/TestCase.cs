using AITestHub.Entities.Enums;

namespace AITestHub.Entities;

/// <summary>
/// Represents a test case generated for a target REST API endpoint.
/// </summary>
public class TestCase
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// HTTP method of the request (GET, POST, PUT, PATCH, DELETE).
    /// </summary>
    public string HttpMethod { get; set; } = string.Empty;

    /// <summary>
    /// Target endpoint path (e.g. /api/users/1).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON request body.
    /// </summary>
    public string? RequestBody { get; set; }

    /// <summary>
    /// Expected HTTP status code of the response.
    /// </summary>
    public int ExpectedStatusCode { get; set; }

    /// <summary>
    /// Expected result in natural language (populated for AI-generated test cases).
    /// </summary>
    public string? ExpectedResult { get; set; }

    /// <summary>
    /// Preconditions that must be satisfied before running the test.
    /// </summary>
    public string? Preconditions { get; set; }

    /// <summary>
    /// Step-by-step instructions to execute the test.
    /// </summary>
    public string Steps { get; set; } = string.Empty;

    /// <summary>
    /// Priority of the test case.
    /// </summary>
    public Priority Priority { get; set; } = Priority.Medium;

    /// <summary>
    /// Type of the test scenario (positive, negative, edge case).
    /// </summary>
    public TestType TestType { get; set; } = TestType.Positive;

    /// <summary>
    /// Current execution status of the test case.
    /// </summary>
    public TestStatus Status { get; set; } = TestStatus.NotRun;

    /// <summary>
    /// Notes captured during the last execution.
    /// </summary>
    public string? ExecutionNotes { get; set; }

    /// <summary>
    /// Timestamp of the last execution (UTC), null if never executed.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Execution history produced by the automated test executor.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public ICollection<TestExecutionResult> ExecutionResults { get; set; } = new List<TestExecutionResult>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Foreign key to the project this test case belongs to.
    /// </summary>
    public int ProjectId { get; set; }

    /// <summary>
    /// Navigation property to the owning project.
    /// ValidateNever: a non-nullable navigation property would otherwise be treated as
    /// implicitly required by MVC model validation and reject requests that only send ProjectId.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public Project Project { get; set; } = null!;
}

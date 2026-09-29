using AITestHub.Entities;
using AITestHub.Entities.Enums;

namespace AITestHub.DTOs;

/// <summary>
/// API representation of a stored TestCase. Used instead of exposing the EF Core entity directly.
/// </summary>
public class TestCaseResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Preconditions { get; set; }

    public string Steps { get; set; } = string.Empty;

    public string? ExpectedResult { get; set; }

    public Priority Priority { get; set; }

    public TestType TestType { get; set; }

    public TestStatus Status { get; set; }

    public string? ExecutionNotes { get; set; }

    public DateTime? LastExecutedAt { get; set; }

    public string HttpMethod { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string? RequestBody { get; set; }

    public int ExpectedStatusCode { get; set; }

    public int ProjectId { get; set; }

    public DateTime CreatedAt { get; set; }

    public static TestCaseResponseDto FromEntity(TestCase testCase) => new()
    {
        Id = testCase.Id,
        Name = testCase.Name,
        Description = testCase.Description,
        Preconditions = testCase.Preconditions,
        Steps = testCase.Steps,
        ExpectedResult = testCase.ExpectedResult,
        Priority = testCase.Priority,
        TestType = testCase.TestType,
        Status = testCase.Status,
        ExecutionNotes = testCase.ExecutionNotes,
        LastExecutedAt = testCase.LastExecutedAt,
        HttpMethod = testCase.HttpMethod,
        Endpoint = testCase.Endpoint,
        RequestBody = testCase.RequestBody,
        ExpectedStatusCode = testCase.ExpectedStatusCode,
        ProjectId = testCase.ProjectId,
        CreatedAt = testCase.CreatedAt
    };
}

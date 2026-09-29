using System.ComponentModel.DataAnnotations;
using AITestHub.Entities.Enums;

namespace AITestHub.DTOs;

/// <summary>
/// Request to record a test execution result for a test case.
/// NotRun is not accepted here; use the reset endpoint instead.
/// </summary>
public class UpdateTestExecutionRequest
{
    [Required(ErrorMessage = "Status is required.")]
    public TestStatus? Status { get; set; }

    [MaxLength(3000)]
    public string? ExecutionNotes { get; set; }
}

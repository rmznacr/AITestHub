using System.ComponentModel.DataAnnotations;

namespace AITestHub.DTOs;

/// <summary>
/// Request to execute a batch of test cases.
/// </summary>
public class BatchExecuteRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one test case id is required.")]
    [MaxLength(50, ErrorMessage = "A maximum of 50 test cases can be executed per batch.")]
    public List<int> TestCaseIds { get; set; } = new();
}

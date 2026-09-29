using System.ComponentModel.DataAnnotations;

namespace AITestHub.DTOs;

/// <summary>
/// Request for saving AI-generated executable API test cases.
/// </summary>
public class SaveGeneratedApiTestCasesRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ProjectId must be a positive integer.")]
    public int ProjectId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one test case is required.")]
    [MaxLength(20, ErrorMessage = "A maximum of 20 test cases can be saved at once.")]
    public List<GeneratedApiTestCaseDto> TestCases { get; set; } = new();
}

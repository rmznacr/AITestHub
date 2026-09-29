using System.ComponentModel.DataAnnotations;

namespace AITestHub.DTOs;

/// <summary>
/// Request to persist user-approved (selected) AI-generated test cases for a project.
/// </summary>
public class SaveGeneratedTestCasesRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ProjectId must be greater than 0.")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "TestCases is required.")]
    [MinLength(1, ErrorMessage = "At least one test case must be provided.")]
    [MaxLength(20, ErrorMessage = "At most 20 test cases can be saved at once.")]
    public List<GeneratedTestCaseDto> TestCases { get; set; } = [];
}

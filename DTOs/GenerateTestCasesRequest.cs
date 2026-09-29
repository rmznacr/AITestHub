using System.ComponentModel.DataAnnotations;

namespace AITestHub.DTOs;

/// <summary>
/// Request to generate test cases for a project's requirement using AI.
/// </summary>
public class GenerateTestCasesRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ProjectId must be greater than 0.")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "Requirement is required.")]
    [MaxLength(5000, ErrorMessage = "Requirement must be at most 5000 characters.")]
    public string Requirement { get; set; } = string.Empty;

    [Range(1, 20, ErrorMessage = "Count must be between 1 and 20.")]
    public int Count { get; set; } = 5;
}

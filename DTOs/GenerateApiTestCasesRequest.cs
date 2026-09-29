using System.ComponentModel.DataAnnotations;

namespace AITestHub.DTOs;

/// <summary>
/// Request for generating executable API test cases with AI.
/// </summary>
public class GenerateApiTestCasesRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "ProjectId must be a positive integer.")]
    public int ProjectId { get; set; }

    [Required(ErrorMessage = "BaseUrl is required.")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Endpoint is required.")]
    public string Endpoint { get; set; } = string.Empty;

    [Required(ErrorMessage = "HttpMethod is required.")]
    public string HttpMethod { get; set; } = string.Empty;

    [Required(ErrorMessage = "Requirement is required.")]
    [MaxLength(5000, ErrorMessage = "Requirement must not exceed 5000 characters.")]
    public string Requirement { get; set; } = string.Empty;

    [Range(1, 20, ErrorMessage = "Count must be between 1 and 20.")]
    public int Count { get; set; } = 5;
}

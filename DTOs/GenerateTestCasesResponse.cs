namespace AITestHub.DTOs;

/// <summary>
/// Response containing the generated test cases for a requirement.
/// </summary>
public class GenerateTestCasesResponse
{
    public int ProjectId { get; set; }

    public string Requirement { get; set; } = string.Empty;

    public List<GeneratedTestCaseDto> TestCases { get; set; } = [];
}

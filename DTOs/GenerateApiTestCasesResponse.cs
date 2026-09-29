namespace AITestHub.DTOs;

/// <summary>
/// Response of the executable API test case generation. Nothing is persisted.
/// </summary>
public class GenerateApiTestCasesResponse
{
    public int ProjectId { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string Requirement { get; set; } = string.Empty;

    public List<GeneratedApiTestCaseDto> TestCases { get; set; } = new();
}

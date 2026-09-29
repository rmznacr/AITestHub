namespace AITestHub.DTOs;

/// <summary>
/// Response returned after persisting AI-generated test cases.
/// </summary>
public class SaveGeneratedTestCasesResponse
{
    public int ProjectId { get; set; }

    public int SavedCount { get; set; }

    public List<TestCaseResponseDto> TestCases { get; set; } = [];
}

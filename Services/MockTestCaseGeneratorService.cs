using AITestHub.DTOs;

namespace AITestHub.Services;

/// <summary>
/// Temporary mock implementation of ITestCaseGeneratorService.
/// Does not use any LLM; produces placeholder test cases that can later
/// be replaced by a real AI-backed implementation.
/// </summary>
public class MockTestCaseGeneratorService : ITestCaseGeneratorService
{
    private readonly ILogger<MockTestCaseGeneratorService> _logger;

    public MockTestCaseGeneratorService(ILogger<MockTestCaseGeneratorService> logger)
    {
        _logger = logger;
    }

    public Task<List<GeneratedTestCaseDto>> GenerateAsync(
        string requirement,
        int count,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {Count} mock test cases for requirement: {Requirement}", count, requirement);

        var testCases = Enumerable.Range(1, count)
            .Select(i => new GeneratedTestCaseDto
            {
                Title = $"Generated Test Case {i}",
                Description = $"Test scenario generated for: {requirement}",
                Preconditions = "The system is accessible and the requirement is implemented.",
                Steps = $"1. Review the requirement.\n2. Execute the scenario for: {requirement}\n3. Observe the result.",
                ExpectedResult = "The requirement should behave as expected.",
                Priority = Entities.Enums.Priority.Medium,
                TestType = i % 3 == 1 ? Entities.Enums.TestType.Positive
                    : i % 3 == 2 ? Entities.Enums.TestType.Negative
                    : Entities.Enums.TestType.EdgeCase
            })
            .ToList();

        return Task.FromResult(testCases);
    }
}

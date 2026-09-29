using AITestHub.DTOs;

namespace AITestHub.Services;

/// <summary>
/// Abstraction for AI-powered test case generation.
/// The current implementation is a mock; it will later be replaced
/// by a real LLM-backed implementation (e.g. EVREN/OpenAI/Gemini).
/// </summary>
public interface ITestCaseGeneratorService
{
    Task<List<GeneratedTestCaseDto>> GenerateAsync(
        string requirement,
        int count,
        CancellationToken cancellationToken = default);
}

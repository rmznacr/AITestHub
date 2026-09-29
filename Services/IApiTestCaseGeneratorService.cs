using AITestHub.DTOs;

namespace AITestHub.Services;

/// <summary>
/// Generates EXECUTABLE API test cases with AI: in addition to the manual test
/// case fields, the output contains the HTTP request definition (method, endpoint,
/// request body, expected status code) required by the automated test executor.
/// </summary>
public interface IApiTestCaseGeneratorService
{
    Task<List<GeneratedApiTestCaseDto>> GenerateAsync(
        GenerateApiTestCasesRequest request,
        CancellationToken cancellationToken = default);
}

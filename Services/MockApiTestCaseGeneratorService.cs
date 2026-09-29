using AITestHub.DTOs;
using AITestHub.Entities.Enums;

namespace AITestHub.Services;

/// <summary>
/// Deterministic mock implementation of <see cref="IApiTestCaseGeneratorService"/>.
/// Used by the integration test infrastructure so that tests never call the
/// real EVREN API. Endpoints of even-indexed test cases contain the marker
/// segment "fail" so the fake API test executor reports them as failed.
/// </summary>
public class MockApiTestCaseGeneratorService : IApiTestCaseGeneratorService
{
    public Task<List<GeneratedApiTestCaseDto>> GenerateAsync(
        GenerateApiTestCasesRequest request,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = request.BaseUrl.Trim().TrimEnd('/') + "/";
        var relative = request.Endpoint.Trim().TrimStart('/');

        var method = request.HttpMethod.Trim().ToUpperInvariant();

        var carriesBody = method is "POST" or "PUT" or "PATCH";

        var testCases = Enumerable.Range(1, request.Count)
            .Select(i =>
            {
                // Every second case gets a "fail" marker so that the fake
                // executor produces a deterministic mix of passed/failed results.
                var endpoint = baseUrl + relative + (i % 2 == 0 ? $"?case={i}-fail" : $"?case={i}");

                return new GeneratedApiTestCaseDto
                {
                    Title = $"API Test Case {i}",
                    Description = $"Executable API test generated for: {request.Requirement}",
                    Preconditions = "The target API is reachable.",
                    Steps = $"1. Send a {method} request to the endpoint.\n2. Verify the response status code.",
                    ExpectedResult = "The API responds with the expected status code.",
                    Priority = i % 2 == 0 ? Priority.Medium : Priority.High,
                    TestType = i % 3 == 1 ? TestType.Positive : (i % 3 == 2 ? TestType.Negative : TestType.EdgeCase),
                    HttpMethod = method,
                    Endpoint = endpoint,
                    RequestBody = carriesBody ? "{\"name\":\"Test User\",\"email\":\"test@example.com\"}" : null,
                    ExpectedStatusCode = method == "POST" ? 201 : method == "DELETE" ? 204 : 200
                };
            })
            .ToList();

        return Task.FromResult(testCases);
    }
}

using System.Net;
using AITestHub.Entities;
using AITestHub.Services;

namespace AITestHub.Tests.Infrastructure;

/// <summary>
/// Fake DNS resolver for tests: every hostname resolves to a public IP address
/// so endpoint security validation passes without any real DNS lookup.
/// </summary>
public sealed class FakeDnsResolver : IDnsResolver
{
    public Task<IPAddress[]> GetHostAddressesAsync(string host, CancellationToken cancellationToken = default) =>
        Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
}

/// <summary>
/// Fake API test executor for integration tests. No real HTTP requests are sent.
///
/// Behavior (deterministic, driven by the test case data):
/// - If the endpoint contains "fail", the request "fails": the actual status code
///   is 500 unless the expected status code is 500 (in which case 404 is returned).
/// - Otherwise the actual status code equals the expected status code -> Passed.
/// </summary>
public sealed class FakeApiTestExecutorService : IApiTestExecutorService
{
    public const int FakeDurationMs = 42;
    public const string FakeResponseBody = "{\"fake\":true}";

    public Task<ApiTestExecutionResult> ExecuteAsync(TestCase testCase, CancellationToken cancellationToken = default)
    {
        var failed = (testCase.Endpoint ?? string.Empty).Contains("fail", StringComparison.OrdinalIgnoreCase);

        var actualStatusCode = failed
            ? (testCase.ExpectedStatusCode == 500 ? 404 : 500)
            : testCase.ExpectedStatusCode;

        var result = new ApiTestExecutionResult
        {
            Passed = actualStatusCode == testCase.ExpectedStatusCode,
            ExecutedAt = DateTime.UtcNow,
            DurationMs = FakeDurationMs,
            RequestMethod = string.IsNullOrWhiteSpace(testCase.HttpMethod)
                ? "UNKNOWN"
                : testCase.HttpMethod.Trim().ToUpperInvariant(),
            RequestUrl = testCase.Endpoint ?? string.Empty,
            RequestBody = testCase.RequestBody,
            ActualStatusCode = actualStatusCode,
            ResponseBody = FakeResponseBody,
            ErrorMessage = actualStatusCode == testCase.ExpectedStatusCode
                ? null
                : $"Expected HTTP {testCase.ExpectedStatusCode}, received {actualStatusCode}."
        };

        return Task.FromResult(result);
    }
}

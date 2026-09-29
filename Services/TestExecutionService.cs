using AITestHub.Data;
using AITestHub.DTOs;
using AITestHub.Entities;
using AITestHub.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace AITestHub.Services;

/// <summary>
/// Coordinates automated test execution: runs the HTTP executor, records the
/// execution history and updates the test case status in a single save.
/// </summary>
public interface ITestExecutionService
{
    /// <summary>
    /// Executes the test case, persists the execution history entry and
    /// updates Status / ExecutionNotes / LastExecutedAt. Never throws for
    /// test-level failures (they are returned as failed results).
    /// </summary>
    Task<ExecuteTestCaseResponseDto> ExecuteAndPersistAsync(TestCase testCase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the execution history of a test case, newest first.
    /// </summary>
    Task<List<ExecutionHistoryDto>> GetHistoryAsync(int testCaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single execution detail entry, or null when not found.
    /// </summary>
    Task<ExecutionDetailDto?> GetExecutionDetailAsync(int executionId, CancellationToken cancellationToken = default);
}

public class TestExecutionService : ITestExecutionService
{
    private readonly AppDbContext _context;
    private readonly IApiTestExecutorService _executor;

    public TestExecutionService(AppDbContext context, IApiTestExecutorService executor)
    {
        _context = context;
        _executor = executor;
    }

    public async Task<ExecuteTestCaseResponseDto> ExecuteAndPersistAsync(
        TestCase testCase,
        CancellationToken cancellationToken = default)
    {
        var result = await _executor.ExecuteAsync(testCase, cancellationToken);

        var executionResult = new TestExecutionResult
        {
            TestCaseId = testCase.Id,
            ExecutedAt = result.ExecutedAt,
            DurationMs = result.DurationMs,
            RequestMethod = result.RequestMethod,
            RequestUrl = result.RequestUrl,
            RequestBody = result.RequestBody,
            ActualStatusCode = result.ActualStatusCode,
            ExpectedStatusCode = testCase.ExpectedStatusCode,
            ResponseBody = result.ResponseBody,
            Passed = result.Passed,
            ErrorMessage = result.ErrorMessage
        };

        _context.TestExecutionResults.Add(executionResult);

        testCase.Status = result.Passed ? TestStatus.Passed : TestStatus.Failed;
        testCase.LastExecutedAt = result.ExecutedAt;
        testCase.ExecutionNotes = result.Passed
            ? $"Automated execution passed. Expected HTTP {testCase.ExpectedStatusCode}, received {result.ActualStatusCode}."
            : $"Automated execution failed. {(result.ActualStatusCode > 0
                ? $"Expected HTTP {testCase.ExpectedStatusCode}, received {result.ActualStatusCode}."
                : "The request did not complete.")}";

        await _context.SaveChangesAsync(cancellationToken);

        return ExecuteTestCaseResponseDto.FromEntity(executionResult, testCase.Status);
    }

    public async Task<List<ExecutionHistoryDto>> GetHistoryAsync(
        int testCaseId,
        CancellationToken cancellationToken = default)
    {
        var history = await _context.TestExecutionResults
            .AsNoTracking()
            .Where(r => r.TestCaseId == testCaseId)
            .OrderByDescending(r => r.ExecutedAt)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        return history.Select(ExecutionHistoryDto.FromEntity).ToList();
    }

    public async Task<ExecutionDetailDto?> GetExecutionDetailAsync(
        int executionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _context.TestExecutionResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == executionId, cancellationToken);

        return result is null ? null : ExecutionDetailDto.FromEntity(result);
    }
}

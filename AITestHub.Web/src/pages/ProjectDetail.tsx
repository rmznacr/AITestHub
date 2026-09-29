import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { getProject, getProjectTestSummary, executeProjectTests } from '../api/projects';
import { getTestCases, executeTestCase } from '../api/testCases';
import type { Project, ProjectTestSummary, TestCaseResponse, BatchExecuteResponse, ExecuteTestCaseResponse } from '../types';
import { SummaryCards, PassRateBar } from '../components/SummaryCards';
import { StatusBadge, PriorityBadge, TypeBadge, MethodBadge } from '../components/Badges';
import { Loading, ErrorState, EmptyState } from '../components/States';
import { formatDateTime } from '../utils/summary';
import type { TestStatus, Priority, TestType } from '../types';

export default function ProjectDetail() {
  const { projectId } = useParams<{ projectId: string }>();
  const id = Number(projectId);

  const [project, setProject] = useState<Project | null>(null);
  const [summary, setSummary] = useState<ProjectTestSummary | null>(null);
  const [testCases, setTestCases] = useState<TestCaseResponse[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [statusFilter, setStatusFilter] = useState<TestStatus | ''>('');
  const [priorityFilter, setPriorityFilter] = useState<Priority | ''>('');
  const [typeFilter, setTypeFilter] = useState<TestType | ''>('');

  const [executingId, setExecutingId] = useState<number | null>(null);
  const [executingAll, setExecutingAll] = useState(false);
  const [lastResult, setLastResult] = useState<ExecuteTestCaseResponse | null>(null);
  const [batchResult, setBatchResult] = useState<BatchExecuteResponse | null>(null);

  const refresh = useCallback(() => {
    Promise.all([getProject(id), getProjectTestSummary(id), getTestCases(id)])
      .then(([projectData, summaryData, testCasesData]) => {
        setProject(projectData);
        setSummary(summaryData);
        setTestCases(testCasesData);
        setError(null);
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const executeOne = (testCaseId: number) => {
    if (executingId !== null) return;
    setExecutingId(testCaseId);
    setLastResult(null);
    setBatchResult(null);
    executeTestCase(testCaseId)
      .then((result) => setLastResult(result))
      .catch((err: Error) => setError(err.message))
      .finally(() => {
        setExecutingId(null);
        refresh();
      });
  };

  const executeAll = () => {
    if (executingAll) return;
    setExecutingAll(true);
    setLastResult(null);
    setBatchResult(null);
    executeProjectTests(id)
      .then((result) => setBatchResult(result))
      .catch((err: Error) => setError(err.message))
      .finally(() => {
        setExecutingAll(false);
        refresh();
      });
  };

  if (loading) return <Loading />;
  if (error) return <ErrorState message={error} />;
  if (!project || !testCases) return null;

  const filtered = testCases.filter(
    (tc) =>
      (statusFilter === '' || tc.status === statusFilter) &&
      (priorityFilter === '' || tc.priority === priorityFilter) &&
      (typeFilter === '' || tc.testType === typeFilter),
  );

  return (
    <div>
      <div className="page-header">
        <div>
          <h2>{project.name}</h2>
          <p className="cell-muted">{project.description || 'No description'}</p>
        </div>
        <div className="page-actions">
          <Link className="btn btn-primary" to={`/projects/${id}/generate`}>
            Generate AI Tests
          </Link>
          <button className="btn" onClick={executeAll} disabled={executingAll || testCases.length === 0}>
            {executingAll ? 'Executing…' : 'Execute All'}
          </button>
        </div>
      </div>

      {summary ? (
        <>
          <SummaryCards
            total={summary.totalTests}
            passed={summary.passed}
            failed={summary.failed}
            blocked={summary.blocked}
            notRun={summary.notRun}
          />
          <PassRateBar passRate={summary.passRate} />
        </>
      ) : null}

      {lastResult ? (
        <div className={`card result-card ${lastResult.passed ? 'result-passed' : 'result-failed'}`}>
          <strong>{lastResult.passed ? 'PASSED' : 'FAILED'}</strong> — {lastResult.requestMethod}{' '}
          {lastResult.requestUrl}: expected {lastResult.expectedStatusCode}, got{' '}
          {lastResult.actualStatusCode} in {lastResult.durationMs} ms.
          {lastResult.errorMessage ? <div className="cell-muted">{lastResult.errorMessage}</div> : null}
        </div>
      ) : null}

      {batchResult ? (
        <div className="card result-card">
          <strong>Batch executed:</strong> {batchResult.total} test(s) — {batchResult.passed} passed,{' '}
          {batchResult.failed} failed.
        </div>
      ) : null}

      <div className="filters">
        <label>
          Status{' '}
          <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as TestStatus | '')}>
            <option value="">All</option>
            <option value="NotRun">NotRun</option>
            <option value="Passed">Passed</option>
            <option value="Failed">Failed</option>
            <option value="Blocked">Blocked</option>
          </select>
        </label>
        <label>
          Priority{' '}
          <select value={priorityFilter} onChange={(e) => setPriorityFilter(e.target.value as Priority | '')}>
            <option value="">All</option>
            <option value="Low">Low</option>
            <option value="Medium">Medium</option>
            <option value="High">High</option>
            <option value="Critical">Critical</option>
          </select>
        </label>
        <label>
          Type{' '}
          <select value={typeFilter} onChange={(e) => setTypeFilter(e.target.value as TestType | '')}>
            <option value="">All</option>
            <option value="Positive">Positive</option>
            <option value="Negative">Negative</option>
            <option value="EdgeCase">EdgeCase</option>
          </select>
        </label>
      </div>

      {filtered.length === 0 ? (
        <EmptyState
          title={testCases.length === 0 ? 'No test cases yet' : 'No test cases match the filters'}
          hint={
            testCases.length === 0
              ? 'Use "Generate AI Tests" to create executable test cases with AI.'
              : undefined
          }
        />
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Method</th>
              <th>Endpoint</th>
              <th>Priority</th>
              <th>Type</th>
              <th>Status</th>
              <th>Expected</th>
              <th>Last Executed</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((tc) => (
              <tr key={tc.id}>
                <td>
                  <Link to={`/testcases/${tc.id}`}>{tc.name}</Link>
                </td>
                <td>
                  <MethodBadge method={tc.httpMethod || '—'} />
                </td>
                <td className="cell-muted cell-ellipsis" title={tc.endpoint}>
                  {tc.endpoint || '—'}
                </td>
                <td>
                  <PriorityBadge priority={tc.priority} />
                </td>
                <td>
                  <TypeBadge testType={tc.testType} />
                </td>
                <td>
                  <StatusBadge status={tc.status} />
                </td>
                <td>{tc.expectedStatusCode || '—'}</td>
                <td className="cell-muted">{formatDateTime(tc.lastExecutedAt)}</td>
                <td>
                  <button
                    className="btn btn-small"
                    onClick={() => executeOne(tc.id)}
                    disabled={executingId !== null || executingAll}
                  >
                    {executingId === tc.id ? 'Running…' : 'Execute'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

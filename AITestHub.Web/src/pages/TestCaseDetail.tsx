import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { getTestCase, getExecutionHistory, getExecutionDetail, executeTestCase } from '../api/testCases';
import type { TestCaseResponse, ExecutionHistory, ExecutionDetail, ExecuteTestCaseResponse } from '../types';
import { StatusBadge, PriorityBadge, TypeBadge, MethodBadge } from '../components/Badges';
import { Loading, ErrorState, EmptyState } from '../components/States';
import { formatDateTime } from '../utils/summary';

export default function TestCaseDetail() {
  const { testCaseId } = useParams<{ testCaseId: string }>();
  const id = Number(testCaseId);

  const [testCase, setTestCase] = useState<TestCaseResponse | null>(null);
  const [history, setHistory] = useState<ExecutionHistory[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const [executing, setExecuting] = useState(false);
  const [lastResult, setLastResult] = useState<ExecuteTestCaseResponse | null>(null);
  const [expandedExecutionId, setExpandedExecutionId] = useState<number | null>(null);
  const [executionDetail, setExecutionDetail] = useState<ExecutionDetail | null>(null);

  const refresh = useCallback(() => {
    Promise.all([getTestCase(id), getExecutionHistory(id)])
      .then(([testCaseData, historyData]) => {
        setTestCase(testCaseData);
        setHistory(historyData);
        setError(null);
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const execute = () => {
    if (executing) return;
    setExecuting(true);
    setLastResult(null);
    executeTestCase(id)
      .then((result) => setLastResult(result))
      .catch((err: Error) => setError(err.message))
      .finally(() => {
        setExecuting(false);
        refresh();
      });
  };

  const toggleHistoryItem = (executionId: number) => {
    if (expandedExecutionId === executionId) {
      setExpandedExecutionId(null);
      setExecutionDetail(null);
      return;
    }
    setExpandedExecutionId(executionId);
    setExecutionDetail(null);
    getExecutionDetail(executionId)
      .then(setExecutionDetail)
      .catch((err: Error) => setError(err.message));
  };

  if (loading) return <Loading />;
  if (error && !testCase) return <ErrorState message={error} />;
  if (!testCase) return null;

  return (
    <div>
      <div className="page-header">
        <div>
          <h2>{testCase.name}</h2>
          <p className="cell-muted">{testCase.description || 'No description'}</p>
        </div>
        <div className="page-actions">
          <Link className="btn" to={`/projects/${testCase.projectId}`}>
            Back to Project
          </Link>
          <button className="btn btn-primary" onClick={execute} disabled={executing}>
            {executing ? 'Executing…' : 'Execute'}
          </button>
        </div>
      </div>

      {error ? <ErrorState message={error} /> : null}

      {lastResult ? (
        <div className={`card result-card ${lastResult.passed ? 'result-passed' : 'result-failed'}`}>
          <strong>{lastResult.passed ? 'PASSED' : 'FAILED'}</strong> — expected{' '}
          {lastResult.expectedStatusCode}, got {lastResult.actualStatusCode} in{' '}
          {lastResult.durationMs} ms.
          {lastResult.errorMessage ? <div className="cell-muted">{lastResult.errorMessage}</div> : null}
        </div>
      ) : null}

      <div className="card">
        <h3>Details</h3>
        <dl className="detail-list">
          <div>
            <dt>Status</dt>
            <dd>
              <StatusBadge status={testCase.status} />
            </dd>
          </div>
          <div>
            <dt>Priority</dt>
            <dd>
              <PriorityBadge priority={testCase.priority} />
            </dd>
          </div>
          <div>
            <dt>Type</dt>
            <dd>
              <TypeBadge testType={testCase.testType} />
            </dd>
          </div>
          <div>
            <dt>HTTP Method</dt>
            <dd>
              <MethodBadge method={testCase.httpMethod || '—'} />
            </dd>
          </div>
          <div>
            <dt>Endpoint</dt>
            <dd>
              <code>{testCase.endpoint || '—'}</code>
            </dd>
          </div>
          <div>
            <dt>Expected Status Code</dt>
            <dd>{testCase.expectedStatusCode || '—'}</dd>
          </div>
          <div>
            <dt>Created</dt>
            <dd>{formatDateTime(testCase.createdAt)}</dd>
          </div>
          <div>
            <dt>Last Executed</dt>
            <dd>{formatDateTime(testCase.lastExecutedAt)}</dd>
          </div>
        </dl>

        {testCase.preconditions ? (
          <>
            <h4>Preconditions</h4>
            <p>{testCase.preconditions}</p>
          </>
        ) : null}

        <h4>Steps</h4>
        <pre className="pre-wrap">{testCase.steps || '—'}</pre>

        {testCase.expectedResult ? (
          <>
            <h4>Expected Result</h4>
            <p>{testCase.expectedResult}</p>
          </>
        ) : null}

        {testCase.requestBody ? (
          <>
            <h4>Request Body</h4>
            <pre className="pre-wrap">{testCase.requestBody}</pre>
          </>
        ) : null}

        <h4>Execution Notes</h4>
        <p className="cell-muted">{testCase.executionNotes || 'No execution notes.'}</p>
      </div>

      <div className="card">
        <h3>Execution History</h3>
        {history === null ? (
          <Loading label="Loading history…" />
        ) : history.length === 0 ? (
          <EmptyState title="No executions yet" hint='Press "Execute" to run this test case.' />
        ) : (
          <>
            <table className="table">
              <thead>
                <tr>
                  <th>Executed At</th>
                  <th>Passed</th>
                  <th>Expected</th>
                  <th>Actual</th>
                  <th>Duration</th>
                  <th>Error</th>
                </tr>
              </thead>
              <tbody>
                {history.map((item) => (
                  <tr
                    key={item.id}
                    className={expandedExecutionId === item.id ? 'row-selected' : 'clickable'}
                    onClick={() => toggleHistoryItem(item.id)}
                  >
                    <td>{formatDateTime(item.executedAt)}</td>
                    <td>{item.passed ? '✅' : '❌'}</td>
                    <td>{item.expectedStatusCode}</td>
                    <td>{item.actualStatusCode}</td>
                    <td>{item.durationMs} ms</td>
                    <td className="cell-muted cell-ellipsis" title={item.errorMessage ?? ''}>
                      {item.errorMessage || '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {expandedExecutionId !== null ? (
              <div className="execution-detail">
                <h4>Execution Detail #{expandedExecutionId}</h4>
                {executionDetail === null ? (
                  <Loading label="Loading detail…" />
                ) : (
                  <div>
                    <p>
                      <span className="field-label">Request:</span>{' '}
                      <MethodBadge method={executionDetail.requestMethod} />{' '}
                      <code>{executionDetail.requestUrl}</code>
                    </p>
                    {executionDetail.requestBody ? (
                      <>
                        <span className="field-label">Request Body:</span>
                        <pre className="pre-wrap">{executionDetail.requestBody}</pre>
                      </>
                    ) : null}
                    {executionDetail.responseBody ? (
                      <>
                        <span className="field-label">Response Body:</span>
                        <pre className="pre-wrap">{executionDetail.responseBody}</pre>
                      </>
                    ) : null}
                  </div>
                )}
              </div>
            ) : null}
          </>
        )}
      </div>
    </div>
  );
}

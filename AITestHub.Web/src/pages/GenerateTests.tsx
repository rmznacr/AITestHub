import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { generateApiTestCases, saveGeneratedApiTestCases } from '../api/aiTests';
import type { GeneratedApiTestCase } from '../types';
import { PriorityBadge, TypeBadge, MethodBadge } from '../components/Badges';
import { ErrorState, EmptyState } from '../components/States';
import { toggleSelection, selectAll, selectNone, isAllSelected } from '../utils/selection';

const HTTP_METHODS = ['GET', 'POST', 'PUT', 'PATCH', 'DELETE'] as const;

export default function GenerateTests() {
  const { projectId } = useParams<{ projectId: string }>();
  const id = Number(projectId);
  const navigate = useNavigate();

  const [baseUrl, setBaseUrl] = useState('');
  const [endpoint, setEndpoint] = useState('');
  const [httpMethod, setHttpMethod] = useState<string>('GET');
  const [requirement, setRequirement] = useState('');
  const [count, setCount] = useState(5);

  const [generating, setGenerating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [generated, setGenerated] = useState<GeneratedApiTestCase[] | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());

  const generate = (event: React.FormEvent) => {
    event.preventDefault();
    if (generating) return;
    setGenerating(true);
    setError(null);
    setGenerated(null);
    generateApiTestCases({ projectId: id, baseUrl, endpoint, httpMethod, requirement, count })
      .then((response) => {
        setGenerated(response.testCases);
        setSelected(selectAll(response.testCases.length));
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setGenerating(false));
  };

  const save = () => {
    if (saving || !generated) return;
    const chosen = generated.filter((_, index) => selected.has(index));
    if (chosen.length === 0) {
      setError('Select at least one test case to save.');
      return;
    }
    setSaving(true);
    setError(null);
    saveGeneratedApiTestCases({ projectId: id, testCases: chosen })
      .then(() => navigate(`/projects/${id}`))
      .catch((err: Error) => setError(err.message))
      .finally(() => setSaving(false));
  };

  return (
    <div>
      <div className="page-header">
        <div>
          <h2>Generate AI Tests</h2>
          <p className="cell-muted">
            The AI generates executable API test cases from your requirement. Nothing is saved
            until you review and save the selection.
          </p>
        </div>
        <div className="page-actions">
          <Link className="btn" to={`/projects/${id}`}>
            Back to Project
          </Link>
        </div>
      </div>

      <div className="card">
        <form className="form-grid" onSubmit={generate}>
          <label>
            Base URL
            <input
              placeholder="https://api.example.com"
              value={baseUrl}
              onChange={(e) => setBaseUrl(e.target.value)}
              required
            />
          </label>
          <label>
            Endpoint
            <input
              placeholder="/api/login"
              value={endpoint}
              onChange={(e) => setEndpoint(e.target.value)}
              required
            />
          </label>
          <label>
            HTTP Method
            <select value={httpMethod} onChange={(e) => setHttpMethod(e.target.value)}>
              {HTTP_METHODS.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </label>
          <label>
            Count (1–20)
            <input
              type="number"
              min={1}
              max={20}
              value={count}
              onChange={(e) => setCount(Number(e.target.value))}
            />
          </label>
          <label className="form-span">
            Requirement
            <textarea
              placeholder="Valid credentials return 200. Invalid password returns 401."
              value={requirement}
              onChange={(e) => setRequirement(e.target.value)}
              maxLength={5000}
              rows={4}
              required
            />
          </label>
          <div className="form-span">
            <button className="btn btn-primary" type="submit" disabled={generating}>
              {generating ? 'Generating…' : 'Generate'}
            </button>
          </div>
        </form>
      </div>

      {error ? <ErrorState message={error} /> : null}

      {generated !== null ? (
        generated.length === 0 ? (
          <EmptyState title="The AI did not generate any test cases" />
        ) : (
          <>
            <div className="page-actions">
              <button
                className="btn"
                onClick={() =>
                  setSelected(isAllSelected(selected, generated.length) ? selectNone() : selectAll(generated.length))
                }
              >
                {isAllSelected(selected, generated.length) ? 'Deselect All' : 'Select All'}
              </button>
              <button className="btn btn-primary" onClick={save} disabled={saving}>
                {saving ? 'Saving…' : `Save Selected (${selected.size})`}
              </button>
            </div>

            <div className="generated-list">
              {generated.map((tc, index) => (
                <div
                  key={index}
                  className={`card generated-card ${selected.has(index) ? 'selected' : ''}`}
                  onClick={() => setSelected(toggleSelection(selected, index))}
                >
                  <div className="generated-card-header">
                    <input
                      type="checkbox"
                      checked={selected.has(index)}
                      onChange={() => setSelected(toggleSelection(selected, index))}
                      onClick={(e) => e.stopPropagation()}
                    />
                    <strong>{tc.title}</strong>
                    <PriorityBadge priority={tc.priority} />
                    <TypeBadge testType={tc.testType} />
                  </div>
                  <p>{tc.description}</p>
                  {tc.preconditions ? (
                    <p>
                      <span className="field-label">Preconditions:</span> {tc.preconditions}
                    </p>
                  ) : null}
                  <p>
                    <span className="field-label">Steps:</span>
                  </p>
                  <pre className="pre-wrap">{tc.steps}</pre>
                  <p>
                    <span className="field-label">Expected result:</span> {tc.expectedResult}
                  </p>
                  <div className="generated-card-request">
                    <MethodBadge method={tc.httpMethod} /> <code>{tc.endpoint}</code> →{' '}
                    <strong>{tc.expectedStatusCode}</strong>
                  </div>
                  {tc.requestBody ? (
                    <pre className="pre-wrap">{tc.requestBody}</pre>
                  ) : null}
                </div>
              ))}
            </div>
          </>
        )
      ) : null}
    </div>
  );
}

import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getDashboardSummary } from '../api/dashboard';
import type { DashboardSummary } from '../types';
import { SummaryCards, PassRateBar } from '../components/SummaryCards';
import { Loading, ErrorState } from '../components/States';

export default function Dashboard() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getDashboardSummary()
      .then((data) => {
        if (!cancelled) {
          setSummary(data);
          setError(null);
        }
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  if (loading) return <Loading />;
  if (error) return <ErrorState message={error} />;
  if (!summary) return null;

  return (
    <div>
      <div className="page-actions">
        <Link className="btn btn-primary" to="/projects">
          View Projects
        </Link>
      </div>

      {summary.totalTests === 0 ? (
        <div className="card">
          <h2>Get started</h2>
          <p>
            Create a project, generate AI test cases for your API and execute them — all results
            will be summarized here.
          </p>
        </div>
      ) : null}

      <SummaryCards
        total={summary.totalTests}
        passed={summary.passed}
        failed={summary.failed}
        blocked={summary.blocked}
        notRun={summary.notRun}
      />

      <div className="card-grid">
        <div className="card stat-card">
          <div className="stat-label">Total Projects</div>
          <div className="stat-value">{summary.totalProjects}</div>
        </div>
        <PassRateBar passRate={summary.passRate} />
      </div>
    </div>
  );
}

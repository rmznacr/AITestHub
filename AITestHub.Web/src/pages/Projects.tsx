import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getProjects, createProject, getProjectTestSummary } from '../api/projects';
import type { Project, ProjectTestSummary } from '../types';
import { Loading, ErrorState, EmptyState } from '../components/States';
import { formatDateTime } from '../utils/summary';

export default function Projects() {
  const [projects, setProjects] = useState<Project[] | null>(null);
  const [summaries, setSummaries] = useState<Record<number, ProjectTestSummary>>({});
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [creating, setCreating] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = () => {
    getProjects()
      .then((data) => {
        setProjects(data);
        setError(null);
        // Fetch per-project test summaries (test counts) in parallel.
        Promise.all(
          data.map((p) =>
            getProjectTestSummary(p.id)
              .then((s) => [p.id, s] as const)
              .catch(() => [p.id, null] as const),
          ),
        ).then((entries) => {
          const map: Record<number, ProjectTestSummary> = {};
          for (const [id, summary] of entries) {
            if (summary) map[id] = summary;
          }
          setSummaries(map);
        });
      })
      .catch((err: Error) => setError(err.message));
  };

  useEffect(load, []);

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (creating) return;
    if (!name.trim()) {
      setFormError('Project name is required.');
      return;
    }
    setCreating(true);
    setFormError(null);
    createProject({ name: name.trim(), description: description.trim() || null })
      .then(() => {
        setName('');
        setDescription('');
        load();
      })
      .catch((err: Error) => setFormError(err.message))
      .finally(() => setCreating(false));
  };

  if (error) return <ErrorState message={error} />;
  if (projects === null) return <Loading />;

  return (
    <div>
      <div className="card">
        <h2>New Project</h2>
        <form className="form-row" onSubmit={handleSubmit}>
          <input
            placeholder="Project name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={200}
          />
          <input
            placeholder="Description (optional)"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={2000}
          />
          <button className="btn btn-primary" type="submit" disabled={creating}>
            {creating ? 'Creating…' : 'Create Project'}
          </button>
        </form>
        {formError ? <div className="state state-error">{formError}</div> : null}
      </div>

      {projects.length === 0 ? (
        <EmptyState title="No projects yet" hint="Create your first project above to get started." />
      ) : (
        <table className="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Description</th>
              <th>Tests</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {projects.map((project) => {
              const summary = summaries[project.id];
              return (
                <tr key={project.id}>
                  <td>
                    <Link to={`/projects/${project.id}`}>{project.name}</Link>
                  </td>
                  <td className="cell-muted">{project.description || '—'}</td>
                  <td>{summary ? `${summary.totalTests} (${summary.passed} passed)` : '—'}</td>
                  <td className="cell-muted">{formatDateTime(project.createdAt)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </div>
  );
}

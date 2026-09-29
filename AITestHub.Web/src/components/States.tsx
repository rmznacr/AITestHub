export function Loading({ label = 'Loading…' }: { label?: string }) {
  return <div className="state state-loading">{label}</div>;
}

export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="state state-empty">
      <div className="state-title">{title}</div>
      {hint ? <div className="state-hint">{hint}</div> : null}
    </div>
  );
}

export function ErrorState({ message }: { message: string }) {
  return <div className="state state-error">{message}</div>;
}

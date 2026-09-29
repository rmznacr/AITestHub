import type { TestStatus, Priority } from '../types';
import { statusClass } from '../utils/summary';

export function StatusBadge({ status }: { status: TestStatus }) {
  return <span className={statusClass(status)}>{status}</span>;
}

export function PriorityBadge({ priority }: { priority: Priority }) {
  return <span className="badge badge-priority">{priority}</span>;
}

export function TypeBadge({ testType }: { testType: string }) {
  return <span className="badge badge-type">{testType}</span>;
}

export function MethodBadge({ method }: { method: string }) {
  return <span className="badge badge-method">{method}</span>;
}

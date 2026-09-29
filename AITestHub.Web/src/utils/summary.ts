import type { TestStatus } from '../types';

/** Passed / (Passed + Failed + Blocked) * 100; 0 when nothing has been executed. */
export function computePassRate(passed: number, failed: number, blocked: number): number {
  const executed = passed + failed + blocked;
  if (executed === 0) {
    return 0;
  }
  return Math.round((passed / executed) * 10000) / 100;
}

export function passRateColor(passRate: number): string {
  if (passRate >= 80) return 'green';
  if (passRate >= 50) return 'orange';
  return 'red';
}

/** Maps a status to a badge CSS class. */
export function statusClass(status: TestStatus): string {
  switch (status) {
    case 'Passed':
      return 'badge badge-passed';
    case 'Failed':
      return 'badge badge-failed';
    case 'Blocked':
      return 'badge badge-blocked';
    default:
      return 'badge badge-notrun';
  }
}

export function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString();
}

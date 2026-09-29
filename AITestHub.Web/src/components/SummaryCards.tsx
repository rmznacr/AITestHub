import type { TestStatus } from '../types';
import { StatusBadge } from './Badges';

interface Card {
  label: string;
  value: number;
  status: TestStatus;
}

export function SummaryCards({
  total,
  passed,
  failed,
  blocked,
  notRun,
}: {
  total: number;
  passed: number;
  failed: number;
  blocked: number;
  notRun: number;
}) {
  const cards: Card[] = [
    { label: 'Total Tests', value: total, status: 'NotRun' },
    { label: 'Passed', value: passed, status: 'Passed' },
    { label: 'Failed', value: failed, status: 'Failed' },
    { label: 'Blocked', value: blocked, status: 'Blocked' },
    { label: 'Not Run', value: notRun, status: 'NotRun' },
  ];

  return (
    <div className="card-grid">
      {cards.map((card) => (
        <div key={card.label} className="card stat-card">
          <div className="stat-label">
            {card.label} <StatusBadge status={card.status} />
          </div>
          <div className="stat-value">{card.value}</div>
        </div>
      ))}
    </div>
  );
}

export function PassRateBar({ passRate }: { passRate: number }) {
  return (
    <div className="card pass-rate-card">
      <div className="stat-label">Pass Rate</div>
      <div className="pass-rate-value">{passRate.toFixed(2)}%</div>
      <div className="pass-rate-bar">
        <div className="pass-rate-fill" style={{ width: `${Math.min(100, Math.max(0, passRate))}%` }} />
      </div>
    </div>
  );
}

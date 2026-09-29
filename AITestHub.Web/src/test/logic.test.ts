import { describe, it, expect } from 'vitest';
import { computePassRate, statusClass } from '../utils/summary';
import { toggleSelection, selectAll, selectNone, isAllSelected } from '../utils/selection';

describe('computePassRate', () => {
  it('should return 0 when nothing was executed', () => {
    expect(computePassRate(0, 0, 0)).toBe(0);
  });

  it('should compute passed / executed * 100', () => {
    expect(computePassRate(2, 1, 1)).toBe(50);
    expect(computePassRate(3, 0, 0)).toBe(100);
  });

  it('should round to two decimals', () => {
    expect(computePassRate(1, 1, 1)).toBe(33.33);
  });
});

describe('statusClass', () => {
  it('should map statuses to badge classes', () => {
    expect(statusClass('Passed')).toBe('badge badge-passed');
    expect(statusClass('Failed')).toBe('badge badge-failed');
    expect(statusClass('Blocked')).toBe('badge badge-blocked');
    expect(statusClass('NotRun')).toBe('badge badge-notrun');
  });
});

describe('selection', () => {
  it('should toggle an index', () => {
    let selected = new Set<number>();
    selected = toggleSelection(selected, 0);
    expect(selected.has(0)).toBe(true);
    selected = toggleSelection(selected, 0);
    expect(selected.has(0)).toBe(false);
  });

  it('should not mutate the original set', () => {
    const original = new Set<number>([1]);
    const next = toggleSelection(original, 2);
    expect(original.has(2)).toBe(false);
    expect(next.has(1)).toBe(true);
    expect(next.has(2)).toBe(true);
  });

  it('should select all indexes', () => {
    const selected = selectAll(4);
    expect(selected.size).toBe(4);
    expect(isAllSelected(selected, 4)).toBe(true);
  });

  it('should treat empty lists as not fully selected', () => {
    expect(isAllSelected(selectNone(), 0)).toBe(false);
  });
});

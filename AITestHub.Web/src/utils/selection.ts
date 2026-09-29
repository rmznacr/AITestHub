/** Pure selection helpers for the generated-test preview list. */

export function toggleSelection(selected: ReadonlySet<number>, index: number): Set<number> {
  const next = new Set(selected);
  if (next.has(index)) {
    next.delete(index);
  } else {
    next.add(index);
  }
  return next;
}

export function selectAll(count: number): Set<number> {
  return new Set(Array.from({ length: count }, (_, i) => i));
}

export function selectNone(): Set<number> {
  return new Set<number>();
}

export function isAllSelected(selected: ReadonlySet<number>, count: number): boolean {
  return count > 0 && selected.size === count;
}

import type { SortingState } from '@tanstack/react-table';

// Gilflux timeframes are toggleable, so a stored sort can name a column that is gone.
// undefined means "never sorted", [] means "deliberately unsorted".
export function resolveSort(
  saved: SortingState | undefined,
  validIds: readonly string[],
  fallback: SortingState,
): SortingState {
  if (!saved) return fallback;
  const kept = saved.filter((entry) => validIds.includes(entry.id));
  if (kept.length === 0 && saved.length > 0) return fallback;
  return kept;
}

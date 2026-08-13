import { useCallback, useMemo } from 'react';
import type { OnChangeFn, SortingState } from '@tanstack/react-table';
import { useUserPrefs, type TableSortKey } from './useUserPrefs';
import { resolveSort } from '../lib/tableSort';

// Drop-in for useState<SortingState> that survives reloads. fallback and validIds
// must be stable references - they are memo/callback deps.
export function usePersistedSort(
  key: TableSortKey,
  fallback: SortingState,
  validIds: readonly string[],
): [SortingState, OnChangeFn<SortingState>] {
  const [prefs, patchPrefs] = useUserPrefs();
  const saved = prefs.tableSort[key];

  const sorting = useMemo(
    () => resolveSort(saved, validIds, fallback),
    [saved, validIds, fallback],
  );

  const setSorting = useCallback<OnChangeFn<SortingState>>(
    (updater) => {
      patchPrefs((prev) => {
        const current = resolveSort(prev.tableSort[key], validIds, fallback);
        const next = typeof updater === 'function' ? updater(current) : updater;
        return { tableSort: { ...prev.tableSort, [key]: next } };
      });
    },
    [key, patchPrefs, validIds, fallback],
  );

  return [sorting, setSorting];
}

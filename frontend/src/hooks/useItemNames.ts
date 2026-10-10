import { queryOptions, useQueries, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import { apiGet } from '../api/client';
import type { Item } from '../api/types';
import { appConfigQuery } from './useAppConfig';

/** Item id (as a string key) -> name. */
export type ItemNameList = Record<string, string>;

// /item/names is served with a one-year immutable Cache-Control, so the URL must always carry
// the current version from /config.
export function itemNamesQuery(version: string | undefined) {
  return queryOptions({
    queryKey: ['item-names', version] as const,
    queryFn: ({ signal }) => apiGet<ItemNameList>(`/item/names?v=${version}`, { signal }),
    enabled: version !== undefined,
    staleTime: Infinity,
    gcTime: Infinity,
  });
}

/** Names found in the list, plus the ids that need a per-item /item/{id} lookup. */
export function splitItemNames(
  ids: number[],
  list: ItemNameList | 'loading' | 'unavailable',
): { names: Map<number, string>; missing: number[] } {
  const names = new Map<number, string>();
  if (list === 'loading') return { names, missing: [] };
  if (list === 'unavailable') return { names, missing: ids };

  const missing: number[] = [];
  for (const id of ids) {
    const name = list[id];
    if (name === undefined) missing.push(id);
    else names.set(id, name);
  }
  return { names, missing };
}

export function useItemNames(ids: number[]): Map<number, string> {
  const uniqueIds = useMemo(
    () => [...new Set(ids.filter((id) => Number.isFinite(id)))],
    [ids],
  );

  const config = useQuery(appConfigQuery);
  const version = config.data?.item_names_version;
  const list = useQuery(itemNamesQuery(version));
  const listPending = config.isPending || (version !== undefined && list.isPending);
  const listState = list.data ?? (listPending ? 'loading' : 'unavailable');

  const { names, missing } = useMemo(
    () => splitItemNames(uniqueIds, listState),
    [uniqueIds, listState],
  );

  const queries = useMemo(
    () =>
      missing.map((id) => ({
        queryKey: ['item', id] as const,
        queryFn: ({ signal }: { signal: AbortSignal }) => apiGet<Item>(`/item/${id}`, { signal }),
        staleTime: Infinity,
      })),
    [missing],
  );

  const combine = useCallback(
    (results: UseQueryResult<Item, Error>[]) => {
      const map = new Map(names);
      results.forEach((result, i) => {
        if (result.data) map.set(missing[i], result.data.name);
      });
      return map;
    },
    [names, missing],
  );

  return useQueries({ queries, combine });
}

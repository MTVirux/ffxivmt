import { useQueries, type UseQueryResult } from '@tanstack/react-query';
import { useCallback, useMemo } from 'react';
import { apiGet } from '../api/client';
import type { Item } from '../api/types';

export function useItemNames(ids: number[]): Map<number, string> {
  const uniqueIds = useMemo(
    () => [...new Set(ids.filter((id) => Number.isFinite(id)))],
    [ids],
  );

  const queries = useMemo(
    () =>
      uniqueIds.map((id) => ({
        queryKey: ['item', id] as const,
        queryFn: ({ signal }: { signal: AbortSignal }) => apiGet<Item>(`/item/${id}`, { signal }),
        staleTime: Infinity,
      })),
    [uniqueIds],
  );

  const combine = useCallback(
    (results: UseQueryResult<Item, Error>[]) => {
      const map = new Map<number, string>();
      results.forEach((result, i) => {
        if (result.data) map.set(uniqueIds[i], result.data.name);
      });
      return map;
    },
    [uniqueIds],
  );

  return useQueries({ queries, combine });
}

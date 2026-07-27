import { useQueries } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { Item } from '../api/types';

export function useItemNames(ids: number[]): Map<number, string> {
  const uniqueIds = [...new Set(ids.filter((id) => Number.isFinite(id)))];

  return useQueries({
    queries: uniqueIds.map((id) => ({
      queryKey: ['item', id] as const,
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        apiGet<Item>(`/item/${id}`, { signal }),
      staleTime: Infinity,
    })),
    combine: (results) => {
      const map = new Map<number, string>();
      results.forEach((result, i) => {
        if (result.data) map.set(uniqueIds[i], result.data.name);
      });
      return map;
    },
  });
}

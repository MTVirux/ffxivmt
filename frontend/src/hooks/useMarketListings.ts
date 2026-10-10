import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { fetchListings, type ListingsProgress } from '../lib/universalisListings';

const FIVE_MINUTES = 5 * 60 * 1000;

export type ListingsTarget = { location: string; worldId?: number };

export function useMarketListings(target: ListingsTarget | null, ids: readonly number[]) {
  const [progress, setProgress] = useState<ListingsProgress>({ done: 0, total: 0 });
  // eslint-disable-next-line @tanstack/query/exhaustive-deps -- setProgress is a stable setter, not a query input
  const query = useQuery({
    queryKey: ['gcSealsListings', target, ids] as const,
    queryFn: ({ signal }) => {
      setProgress({ done: 0, total: 0 });
      return fetchListings(target?.location ?? '', ids, {
        worldId: target?.worldId,
        signal,
        onProgress: setProgress,
      });
    },
    enabled: target !== null && ids.length > 0,
    staleTime: FIVE_MINUTES,
    retry: false,
  });
  return { query, progress };
}

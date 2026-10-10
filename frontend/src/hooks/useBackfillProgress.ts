import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { BackfillProgress } from '../api/types';

export function useBackfillProgress() {
  return useQuery({
    queryKey: ['status-backfill'],
    queryFn: ({ signal }) => apiGet<BackfillProgress>('/status/backfill', { signal }),
    refetchInterval: 60_000,
  });
}

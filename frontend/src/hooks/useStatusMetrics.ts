import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { StatusMetrics } from '../api/types';

export function useStatusMetrics() {
  return useQuery({
    queryKey: ['status-metrics'],
    queryFn: ({ signal }) => apiGet<StatusMetrics>('/status/metrics', { signal }),
    refetchInterval: 60_000,
  });
}

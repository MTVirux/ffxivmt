import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { AppConfig } from '../api/types';
import { TIMEFRAMES } from '../lib/rankingAggregate';

const FALLBACK: AppConfig = {
  gilflux_timeframes: TIMEFRAMES.map((tf) => tf.key),
};

export function useAppConfig(): AppConfig {
  const query = useQuery({
    queryKey: ['app-config'],
    queryFn: ({ signal }) => apiGet<AppConfig>('/config', { signal }),
    staleTime: Infinity,
    retry: 1,
  });
  return query.data ?? FALLBACK;
}

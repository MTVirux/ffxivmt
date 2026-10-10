import { queryOptions, useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { AppConfig } from '../api/types';
import { TIMEFRAMES } from '../lib/rankingAggregate';

const FALLBACK: AppConfig = {
  gilflux_timeframes: [...TIMEFRAMES],
};

export const appConfigQuery = queryOptions({
  queryKey: ['app-config'],
  queryFn: ({ signal }) => apiGet<AppConfig>('/config', { signal }),
  staleTime: Infinity,
  retry: 1,
});

export function useAppConfig(): AppConfig {
  const query = useQuery(appConfigQuery);
  return query.data ?? FALLBACK;
}

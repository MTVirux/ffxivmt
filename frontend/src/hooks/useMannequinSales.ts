import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { apiGetEnvelope } from '../api/client';
import type { MannequinSalesResponse } from '../api/types';

export type MannequinFilters = { hqOnly: boolean; minUnitPrice: number };

function fetchPage(
  location: string,
  { hqOnly, minUnitPrice }: MannequinFilters,
  before: number | null,
  signal: AbortSignal,
) {
  const params = new URLSearchParams({ target_location: location });
  if (before !== null) params.set('before', String(before));
  if (hqOnly) params.set('hq_only', 'true');
  if (minUnitPrice > 0) params.set('min_unit_price', String(minUnitPrice));
  return apiGetEnvelope<MannequinSalesResponse>(`/mannequin_sales?${params}`, { signal });
}

// Polling an infinite query refetches every loaded page, so only the head is polled
// and the page merges it over the history.
export function useMannequinSales(location: string | undefined, filters: MannequinFilters) {
  const { hqOnly, minUnitPrice } = filters;
  const enabled = !!location;

  const head = useQuery({
    queryKey: ['mannequin-sales-head', location, hqOnly, minUnitPrice] as const,
    queryFn: ({ signal }) => fetchPage(location ?? '', { hqOnly, minUnitPrice }, null, signal),
    enabled,
    refetchInterval: 30_000,
  });

  const history = useInfiniteQuery({
    queryKey: ['mannequin-sales', location, hqOnly, minUnitPrice] as const,
    queryFn: ({ pageParam, signal }) =>
      fetchPage(location ?? '', { hqOnly, minUnitPrice }, pageParam, signal),
    initialPageParam: null as number | null,
    getNextPageParam: (last) => last.next_before,
    enabled,
  });

  return { head, history };
}

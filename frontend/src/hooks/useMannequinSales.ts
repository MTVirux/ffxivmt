import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { apiGetEnvelope } from '../api/client';
import type { MannequinSalesResponse } from '../api/types';

export type MannequinQuality = 'all' | 'hq' | 'nq';

// An empty datacenter means every world.
export type MannequinFilters = {
  datacenter: string;
  minUnitPrice: number;
  quality: MannequinQuality;
};

function fetchPage(
  { datacenter, minUnitPrice, quality }: MannequinFilters,
  before: number | null,
  signal: AbortSignal,
) {
  const params = new URLSearchParams();
  if (datacenter) params.set('target_location', datacenter);
  if (before !== null) params.set('before', String(before));
  if (minUnitPrice > 0) params.set('min_unit_price', String(minUnitPrice));
  if (quality !== 'all') params.set('hq', String(quality === 'hq'));
  return apiGetEnvelope<MannequinSalesResponse>(`/mannequin_sales?${params}`, { signal });
}

// Polling an infinite query refetches every loaded page, so only the head is polled
// and the page merges it over the history.
export function useMannequinSales(filters: MannequinFilters) {
  const { datacenter, minUnitPrice, quality } = filters;

  const head = useQuery({
    queryKey: ['mannequin-sales-head', datacenter, minUnitPrice, quality] as const,
    queryFn: ({ signal }) => fetchPage({ datacenter, minUnitPrice, quality }, null, signal),
    refetchInterval: 30_000,
  });

  const history = useInfiniteQuery({
    queryKey: ['mannequin-sales', datacenter, minUnitPrice, quality] as const,
    queryFn: ({ pageParam, signal }) =>
      fetchPage({ datacenter, minUnitPrice, quality }, pageParam, signal),
    initialPageParam: null as number | null,
    getNextPageParam: (last) => last.next_before,
  });

  return { head, history };
}

import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { apiGetEnvelope } from '../api/client';
import type { MannequinSalesResponse } from '../api/types';

export type MannequinQuality = 'all' | 'hq' | 'nq';

// An empty world means the whole datacenter, an empty datacenter every world.
export type MannequinFilters = {
  datacenter: string;
  world: string;
  minUnitPrice: number;
  quality: MannequinQuality;
  buyer: string;
};

export function buildMannequinPath(
  { datacenter, world, minUnitPrice, quality, buyer }: MannequinFilters,
  before: number | null,
): string {
  const params = new URLSearchParams();
  const location = world || datacenter;
  const buyerName = buyer.trim();
  if (location) params.set('target_location', location);
  if (before !== null) params.set('before', String(before));
  if (minUnitPrice > 0) params.set('min_unit_price', String(minUnitPrice));
  if (quality !== 'all') params.set('hq', String(quality === 'hq'));
  if (buyerName) params.set('buyer_name', buyerName);
  return `/mannequin_sales?${params}`;
}

function fetchPage(filters: MannequinFilters, before: number | null, signal: AbortSignal) {
  return apiGetEnvelope<MannequinSalesResponse>(buildMannequinPath(filters, before), { signal });
}

// Polling an infinite query refetches every loaded page, so only the head is polled
// and the page merges it over the history.
export function useMannequinSales(filters: MannequinFilters) {
  const { datacenter, world, minUnitPrice, quality, buyer } = filters;

  const head = useQuery({
    queryKey: ['mannequin-sales-head', datacenter, world, minUnitPrice, quality, buyer] as const,
    queryFn: ({ signal }) =>
      fetchPage({ datacenter, world, minUnitPrice, quality, buyer }, null, signal),
    refetchInterval: 30_000,
  });

  const history = useInfiniteQuery({
    queryKey: ['mannequin-sales', datacenter, world, minUnitPrice, quality, buyer] as const,
    queryFn: ({ pageParam, signal }) =>
      fetchPage({ datacenter, world, minUnitPrice, quality, buyer }, pageParam, signal),
    initialPageParam: null as number | null,
    getNextPageParam: (last) => last.next_before,
  });

  return { head, history };
}

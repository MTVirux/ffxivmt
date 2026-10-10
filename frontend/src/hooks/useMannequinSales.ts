import { useInfiniteQuery, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { apiGetEnvelope } from '../api/client';
import type {
  MannequinSale,
  MannequinSalesAllResponse,
  MannequinSalesResponse,
} from '../api/types';
import {
  accumulateHead,
  filterMannequinSales,
  mannequinScope,
  mergeSales,
  type MannequinFilters,
} from '../lib/mannequin';
import { useWorlds } from './useWorlds';

const POLL_MS = 30_000;
const WINDOW_ROWS = 200;

export type MannequinFeed = {
  rows: MannequinSale[];
  hasMore: boolean;
  loadMore: () => void;
  isLoadingMore: boolean;
  isLoading: boolean;
  isError: boolean;
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

// The whole table is small enough to filter in the browser. Once it outgrows the server's
// row cap it comes back incomplete, and the feed pages the filtered endpoint instead.
export function useMannequinSales(filters: MannequinFilters): MannequinFeed {
  const all = useQuery({
    queryKey: ['mannequin-sales-all'] as const,
    queryFn: ({ signal }) =>
      apiGetEnvelope<MannequinSalesAllResponse>('/mannequin_sales/all', { signal }),
    refetchInterval: POLL_MS,
  });
  const paging = all.data?.complete === false;
  const full = useFullFeed(all, filters);
  const paged = usePagedFeed(filters, paging);
  return paging ? paged : full;
}

function useFullFeed(
  all: UseQueryResult<MannequinSalesAllResponse>,
  filters: MannequinFilters,
): MannequinFeed {
  const worlds = useWorlds();
  const scope = mannequinScope(filters);
  const [shown, setShown] = useState({ scope, count: WINDOW_ROWS });
  if (shown.scope !== scope) setShown({ scope, count: WINDOW_ROWS });
  const count = shown.scope === scope ? shown.count : WINDOW_ROWS;

  const matched = useMemo(
    () => all.data && filterMannequinSales(all.data.data, filters, worlds.data),
    [all.data, filters, worlds.data],
  );
  const rows = useMemo(() => matched?.slice(0, count) ?? [], [matched, count]);
  const waitingOnWorlds = all.data !== undefined && matched === undefined;
  const isError = all.isError || (waitingOnWorlds && worlds.isError);

  return {
    rows,
    hasMore: (matched?.length ?? 0) > count,
    loadMore: () => setShown({ scope, count: count + WINDOW_ROWS }),
    isLoadingMore: false,
    isLoading: matched === undefined && !isError,
    isError,
  };
}

// Polling an infinite query refetches every loaded page, so only the head is polled
// and merged over the history.
function usePagedFeed(filters: MannequinFilters, enabled: boolean): MannequinFeed {
  const { datacenter, world, minUnitPrice, quality, buyer } = filters;

  const head = useQuery({
    queryKey: ['mannequin-sales-head', datacenter, world, minUnitPrice, quality, buyer] as const,
    queryFn: ({ signal }) =>
      fetchPage({ datacenter, world, minUnitPrice, quality, buyer }, null, signal),
    refetchInterval: POLL_MS,
    enabled,
  });

  const history = useInfiniteQuery({
    queryKey: ['mannequin-sales', datacenter, world, minUnitPrice, quality, buyer] as const,
    queryFn: ({ pageParam, signal }) =>
      fetchPage({ datacenter, world, minUnitPrice, quality, buyer }, pageParam, signal),
    initialPageParam: null as number | null,
    getNextPageParam: (last) => last.next_before,
    enabled,
  });

  const headRows = useAccumulatedHead(head.data?.data, mannequinScope(filters));
  const rows = useMemo(
    () => mergeSales(headRows, history.data?.pages.flatMap((p) => p.data) ?? []),
    [headRows, history.data],
  );

  return {
    rows,
    hasMore: history.hasNextPage,
    loadMore: () => void history.fetchNextPage(),
    isLoadingMore: history.isFetchingNextPage,
    isLoading: history.isLoading,
    isError: head.isError || history.isError,
  };
}

// The history's first page is never refetched, so rows pushed off the head between polls
// would otherwise vanish. Derived during render; a new scope starts from nothing.
function useAccumulatedHead(latest: MannequinSale[] | undefined, scope: string): MannequinSale[] {
  const [acc, setAcc] = useState<{
    scope: string;
    latest: MannequinSale[] | undefined;
    rows: MannequinSale[];
  }>({ scope, latest: undefined, rows: [] });

  if (acc.scope === scope && acc.latest === latest) return acc.rows;
  const rows = accumulateHead(acc.scope === scope ? acc.rows : [], latest ?? []);
  setAcc({ scope, latest, rows });
  return rows;
}

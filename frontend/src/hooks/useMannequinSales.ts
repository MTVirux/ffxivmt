import { useInfiniteQuery } from '@tanstack/react-query';
import { apiGetEnvelope } from '../api/client';
import type { MannequinSalesResponse } from '../api/types';

export type MannequinFilters = { hqOnly: boolean; minUnitPrice: number };

export function useMannequinSales(location: string | undefined, filters: MannequinFilters) {
  return useInfiniteQuery({
    queryKey: ['mannequin-sales', location, filters.hqOnly, filters.minUnitPrice] as const,
    queryFn: ({ pageParam, signal }) => {
      const params = new URLSearchParams({ target_location: location ?? '' });
      if (pageParam !== null) params.set('before', String(pageParam));
      if (filters.hqOnly) params.set('hq_only', 'true');
      if (filters.minUnitPrice > 0) params.set('min_unit_price', String(filters.minUnitPrice));
      return apiGetEnvelope<MannequinSalesResponse>(`/mannequin_sales?${params}`, { signal });
    },
    initialPageParam: null as number | null,
    getNextPageParam: (last) => last.next_before,
    enabled: !!location,
    refetchInterval: 30_000,
  });
}

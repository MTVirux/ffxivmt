import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { BuyerSearchRow } from '../api/types';

export function useBuyerSearch({
  buyerName,
  world,
  enabled,
}: {
  buyerName: string;
  world: string;
  enabled: boolean;
}) {
  return useQuery({
    queryKey: ['buyer-search', buyerName, world] as const,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({ buyer_name: buyerName });
      if (world) params.set('world', world);
      return apiGet<BuyerSearchRow[]>(`/search_buyer?${params}`, { signal });
    },
    enabled: enabled && buyerName.length > 0,
    staleTime: 0,
  });
}

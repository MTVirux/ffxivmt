import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { Sale } from '../api/types';

const TWENTY_SECONDS = 20_000;

export function useItemSales(itemId: number | undefined, worldId: number | undefined, limit = 100) {
  return useQuery({
    queryKey: ['item-sales', itemId, worldId, limit] as const,
    queryFn: ({ signal }) =>
      apiGet<Sale[]>(`/item/${itemId!}/sales?world_id=${worldId!}&limit=${limit}`, { signal }),
    enabled:
      itemId !== undefined &&
      Number.isFinite(itemId) &&
      worldId !== undefined &&
      Number.isFinite(worldId),
    // Sales arrive on the ws hot path; a short staleTime keeps fresh prices visible
    // without hammering the API on tab refocus.
    staleTime: TWENTY_SECONDS,
  });
}

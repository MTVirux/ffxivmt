import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { Location, Sale } from '../api/types';

const TWENTY_SECONDS = 20_000;

export function buildSalesPath(itemId: number, location: Location, limit: number): string {
  return `/item/${itemId}/sales?target_location=${encodeURIComponent(location.name)}&limit=${limit}`;
}

export function useItemSales(
  itemId: number | undefined,
  location: Location | undefined,
  limit = 100,
) {
  return useQuery({
    queryKey: ['item-sales', itemId, location?.kind, location?.name, location?.worldId, limit] as const,
    queryFn: ({ signal }) => apiGet<Sale[]>(buildSalesPath(itemId!, location!, limit), { signal }),
    enabled:
      itemId !== undefined &&
      Number.isFinite(itemId) &&
      location !== undefined &&
      location.name.length > 0,
    // Sales arrive on the ws hot path; a short staleTime keeps fresh prices visible
    // without hammering the API on tab refocus.
    staleTime: TWENTY_SECONDS,
  });
}

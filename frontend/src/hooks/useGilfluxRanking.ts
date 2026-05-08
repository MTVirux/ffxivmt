import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { GilfluxRanking, Location } from '../api/types';

const TWENTY_SECONDS = 20_000;

export function useGilfluxRanking(location: Location | undefined, craftedOnly: boolean) {
  return useQuery({
    queryKey: ['gilflux', location?.name, location?.kind, craftedOnly] as const,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({
        target_location: location!.name,
        crafted_only: craftedOnly ? '1' : '0',
      });
      return apiGet<GilfluxRanking[]>(`/gilflux?${params.toString()}`, { signal });
    },
    enabled: location !== undefined && location.name.length > 0,
    // Backend caches for 20s by config (GilfluxOptions.RankingCacheSeconds).
    staleTime: TWENTY_SECONDS,
  });
}

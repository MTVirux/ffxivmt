import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { WorldStructure } from '../api/types';

// Backend caches the world structure for 300s (Ffmt.Core/Configuration/GilfluxOptions.cs);
// align the client `staleTime` so we don't refetch faster than fresh values are produced.
const FIVE_MINUTES = 5 * 60 * 1000;

export function useWorlds() {
  return useQuery({
    queryKey: ['worlds'],
    queryFn: ({ signal }) => apiGet<WorldStructure>('/worlds', { signal }),
    staleTime: FIVE_MINUTES,
  });
}

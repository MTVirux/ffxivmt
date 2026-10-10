import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { GcSealsCatalogue } from '../lib/gcSeals';

// Recipes only change with game patches; the backend caches the catalogue for 24h.
const ONE_HOUR = 60 * 60 * 1000;

export function useGcSealsCatalogue() {
  return useQuery({
    queryKey: ['gcSealsCatalogue'],
    queryFn: ({ signal }) => apiGet<GcSealsCatalogue>('/tools/gc_seals/catalogue', { signal }),
    staleTime: ONE_HOUR,
  });
}

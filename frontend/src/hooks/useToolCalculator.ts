import { useQuery } from '@tanstack/react-query';
import { apiGetEnvelope } from '../api/client';
import type { ToolResponse } from '../api/types';

type Args = {
  searchTerm: string;
  location: string;
  /** Set when the user clicks "Calculate" — prevents firing on every keystroke. */
  enabled: boolean;
};

export function useToolCalculator<TRow>(
  endpoint: string,
  key: string,
  { searchTerm, location, enabled }: Args,
) {
  return useQuery({
    queryKey: [key, endpoint, searchTerm, location] as const,
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({ search_term: searchTerm, location });
      return apiGetEnvelope<ToolResponse<TRow>>(`${endpoint}?${params.toString()}`, { signal });
    },
    enabled: enabled && searchTerm.length > 0 && location.length > 0,
    staleTime: 60_000,
    retry: false,
  });
}

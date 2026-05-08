import { useQuery } from '@tanstack/react-query';
import { apiFetch, ApiError } from '../api/client';
import type { CurrencyEfficiencyResponse } from '../api/types';

type Args = {
  searchTerm: string;
  location: string;
  enabled: boolean;
};

export function useCurrencyEfficiency({ searchTerm, location, enabled }: Args) {
  return useQuery({
    queryKey: ['currency-efficiency', searchTerm, location] as const,
    queryFn: async ({ signal }) => {
      const params = new URLSearchParams({ search_term: searchTerm, location });
      const env = await apiFetch<CurrencyEfficiencyResponse>(
        `/tools/currency_efficiency_calculator?${params.toString()}`,
        { signal },
      );
      if (!env.status) throw new ApiError(0, env, env.message);
      return env;
    },
    enabled: enabled && searchTerm.length > 0 && location.length > 0,
    staleTime: 60_000,
    retry: false,
  });
}

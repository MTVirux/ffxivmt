import { useQuery } from '@tanstack/react-query';
import { apiFetch, ApiError } from '../api/client';
import type { ItemProductProfitResponse } from '../api/types';

type Args = {
  searchTerm: string;
  location: string;
  /** Set when the user clicks "Calculate" — prevents firing on every keystroke. */
  enabled: boolean;
};

export function useItemProductProfit({ searchTerm, location, enabled }: Args) {
  return useQuery({
    queryKey: ['item-product-profit', searchTerm, location] as const,
    queryFn: async ({ signal }) => {
      const params = new URLSearchParams({ search_term: searchTerm, location });
      const env = await apiFetch<ItemProductProfitResponse>(
        `/tools/item_product_profit_calculator?${params.toString()}`,
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

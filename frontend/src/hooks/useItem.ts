import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import type { Item } from '../api/types';

export function useItem(id: number | undefined) {
  return useQuery({
    queryKey: ['item', id] as const,
    queryFn: ({ signal }) => apiGet<Item>(`/item/${id!}`, { signal }),
    enabled: id !== undefined && Number.isFinite(id),
  });
}

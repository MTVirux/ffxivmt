import type { MannequinSale } from '../api/types';

export function mannequinSaleKey(s: MannequinSale): string {
  return `${s.world_id}|${s.item_id}|${s.sale_time}|${s.buyer_name}`;
}

/** Rows that arrived on top since `prev`. Empty on a first load so the table doesn't flash. */
export function newSaleKeys(prev: MannequinSale[] | undefined, next: MannequinSale[]): Set<string> {
  if (!prev || prev.length === 0) return new Set();
  const newest = Math.max(...prev.map((s) => Date.parse(s.sale_time)));
  const seen = new Set(prev.map(mannequinSaleKey));
  return new Set(
    next
      .filter((s) => Date.parse(s.sale_time) >= newest && !seen.has(mannequinSaleKey(s)))
      .map(mannequinSaleKey),
  );
}

import type { MannequinSale } from '../api/types';

export function mannequinSaleKey(s: MannequinSale): string {
  return `${s.world_id}|${s.item_id}|${s.sale_time}|${s.buyer_name}`;
}

/** Head and history rows as one newest-first list, deduped. Head rows win ties. */
export function mergeSales(head: MannequinSale[], history: MannequinSale[]): MannequinSale[] {
  const seen = new Set<string>();
  const merged = [...head, ...history].filter((s) => {
    const key = mannequinSaleKey(s);
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
  return merged.sort((a, b) => Date.parse(b.sale_time) - Date.parse(a.sale_time));
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

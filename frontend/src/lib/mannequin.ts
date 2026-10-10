import type { MannequinSale, WorldStructure } from '../api/types';
import { formatNumber } from './format';

export type MannequinQuality = 'all' | 'hq' | 'nq';

// An empty world means the whole datacenter, an empty datacenter every world.
export type MannequinFilters = {
  datacenter: string;
  world: string;
  minUnitPrice: number;
  quality: MannequinQuality;
  buyer: string;
};

/** One key per filter combination, for state that starts over when the filters change. */
export function mannequinScope(f: MannequinFilters): string {
  return `${f.datacenter}|${f.world}|${f.minUnitPrice}|${f.quality}|${f.buyer}`;
}

// The API binds min_unit_price as an int32.
export const MAX_MIN_UNIT_PRICE = 999_999_999;
export const DEFAULT_MIN_UNIT_PRICE = 10_000_000;

/** Reads a typed price, ignoring commas and anything else that isn't a digit. */
export function parseMinPrice(text: string): number {
  return Math.min(MAX_MIN_UNIT_PRICE, Number(text.replace(/\D/g, '')) || 0);
}

export function formatMinPrice(n: number): string {
  return n > 0 ? formatNumber(n) : '';
}

/**
 * The server's feed filter, applied in the browser. Undefined while a location filter
 * still needs the world tree, so callers can keep showing a loading state.
 */
export function filterMannequinSales(
  rows: MannequinSale[],
  { datacenter, world, minUnitPrice, quality, buyer }: MannequinFilters,
  worlds: WorldStructure | undefined,
): MannequinSale[] | undefined {
  let worldIds: Set<number> | null = null;
  if (world || datacenter) {
    if (!worlds) return undefined;
    worldIds = worldIdsAt(worlds, datacenter, world);
  }
  const buyerText = buyer.trim().toLowerCase();
  return rows.filter(
    (s) =>
      (worldIds === null || worldIds.has(s.world_id)) &&
      (quality === 'all' || s.hq === (quality === 'hq')) &&
      s.unit_price >= minUnitPrice &&
      s.buyer_name.toLowerCase().includes(buyerText),
  );
}

function worldIdsAt(tree: WorldStructure, datacenter: string, world: string): Set<number> {
  const ids = new Set<number>();
  for (const dcs of Object.values(tree)) {
    for (const [dc, members] of Object.entries(dcs)) {
      for (const [id, name] of Object.entries(members)) {
        if (world ? name === world : dc === datacenter) ids.add(Number(id));
      }
    }
  }
  return ids;
}

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

/** Every head row seen so far, so rows pushed off the head between polls stay listed. */
export function accumulateHead(seen: MannequinSale[], latest: MannequinSale[]): MannequinSale[] {
  return mergeSales(latest, seen);
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

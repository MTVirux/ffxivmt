import { describe, expect, it } from 'vitest';
import type { MannequinSale } from '../api/types';
import {
  MAX_MIN_UNIT_PRICE,
  accumulateHead,
  formatMinPrice,
  mannequinSaleKey,
  mergeSales,
  newSaleKeys,
  parseMinPrice,
} from './mannequin';

function sale(sale_time: string, item_id = 1, buyer_name = 'B'): MannequinSale {
  return {
    item_id,
    world_id: 85,
    buyer_name,
    sale_time,
    hq: false,
    quantity: 1,
    unit_price: 100,
    total_price: 100,
  };
}

const t1 = '2026-10-10T12:00:00+00:00';
const t2 = '2026-10-10T12:05:00+00:00';
const t0 = '2026-10-10T11:00:00+00:00';

describe('newSaleKeys', () => {
  it('flags nothing on the first load', () => {
    expect(newSaleKeys(undefined, [sale(t1)]).size).toBe(0);
    expect(newSaleKeys([], [sale(t1)]).size).toBe(0);
  });

  it('flags a sale that arrived on top', () => {
    const fresh = sale(t2);
    expect([...newSaleKeys([sale(t1)], [fresh, sale(t1)])]).toEqual([mannequinSaleKey(fresh)]);
  });

  it('never flags older rows appended by load older', () => {
    expect(newSaleKeys([sale(t1)], [sale(t1), sale(t0)]).size).toBe(0);
  });

  it('flags an unseen sale sharing the newest timestamp', () => {
    const twin = sale(t1, 2);
    expect([...newSaleKeys([sale(t1)], [twin, sale(t1)])]).toEqual([mannequinSaleKey(twin)]);
  });
});

describe('mergeSales', () => {
  it('returns nothing when both lists are empty', () => {
    expect(mergeSales([], [])).toEqual([]);
  });

  it('keeps one copy of a sale present in both lists', () => {
    const shared = sale(t1);
    expect(mergeSales([sale(t2), shared], [{ ...shared }, sale(t0)])).toEqual([
      sale(t2),
      shared,
      sale(t0),
    ]);
  });

  it('returns newest first across both lists', () => {
    const t3 = '2026-10-10T12:10:00+00:00';
    expect(mergeSales([sale(t3), sale(t0)], [sale(t2), sale(t1)]).map((s) => s.sale_time)).toEqual([
      t3,
      t2,
      t1,
      t0,
    ]);
  });

  it('keeps head order on ties', () => {
    const a = sale(t1, 1);
    const b = sale(t1, 2);
    const c = sale(t1, 3);
    expect(mergeSales([b, a], [a, c, b])).toEqual([b, a, c]);
  });
});

describe('accumulateHead', () => {
  it('keeps rows from an earlier poll that a later poll no longer returns', () => {
    expect(accumulateHead([sale(t1)], [sale(t2)])).toEqual([sale(t2), sale(t1)]);
  });

  it('collapses rows returned by both polls', () => {
    expect(accumulateHead([sale(t1), sale(t0)], [sale(t2), sale(t1)])).toEqual([
      sale(t2),
      sale(t1),
      sale(t0),
    ]);
  });
});

describe('min price input', () => {
  it('groups digits with commas', () => {
    expect(formatMinPrice(1_000_000)).toBe('1,000,000');
    expect(formatMinPrice(0)).toBe('');
  });

  it('reads a formatted price back', () => {
    expect(parseMinPrice('1,000,000')).toBe(1_000_000);
    expect(parseMinPrice('')).toBe(0);
  });

  it('caps a price the API cannot accept', () => {
    expect(parseMinPrice('99999999999')).toBe(MAX_MIN_UNIT_PRICE);
  });
});

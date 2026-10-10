import { describe, expect, it } from 'vitest';
import type { MannequinSale } from '../api/types';
import { mannequinSaleKey, newSaleKeys } from './mannequin';

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

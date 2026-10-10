import { describe, expect, it } from 'vitest';
import type { MannequinSale, WorldStructure } from '../api/types';
import {
  MAX_MIN_UNIT_PRICE,
  accumulateHead,
  filterMannequinSales,
  formatMinPrice,
  mannequinSaleKey,
  mannequinScope,
  mergeSales,
  newSaleKeys,
  parseMinPrice,
  type MannequinFilters,
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

const ANY: MannequinFilters = {
  datacenter: '',
  world: '',
  minUnitPrice: 0,
  quality: 'all',
  buyer: '',
};

describe('newSaleKeys', () => {
  it('flags nothing on the first load', () => {
    expect(newSaleKeys(undefined, [sale(t1)]).size).toBe(0);
    expect(newSaleKeys([], [sale(t1)]).size).toBe(0);
  });

  it('flags a sale that arrived on top', () => {
    const fresh = sale(t2);
    expect([...newSaleKeys([sale(t1)], [fresh, sale(t1)])]).toEqual([mannequinSaleKey(fresh)]);
  });

  it('never flags older rows appended by load more', () => {
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

describe('filterMannequinSales', () => {
  const WORLDS: WorldStructure = {
    Europe: {
      Light: { '33': 'Twintania', '66': 'Odin' },
      Chaos: { '39': 'Omega' },
    },
  };

  function row(patch: Partial<MannequinSale>): MannequinSale {
    return { ...sale(t1), ...patch };
  }

  const twintania = row({ world_id: 33 });
  const odin = row({ world_id: 66 });
  const omega = row({ world_id: 39 });

  it('keeps every row in order with no filters, even before the world tree loads', () => {
    const rows = [sale(t2), sale(t1), sale(t0)];
    expect(filterMannequinSales(rows, ANY, undefined)).toEqual(rows);
  });

  it("keeps a datacenter's worlds", () => {
    expect(
      filterMannequinSales([twintania, omega, odin], { ...ANY, datacenter: 'Light' }, WORLDS),
    ).toEqual([twintania, odin]);
  });

  it('keeps only the chosen world', () => {
    expect(
      filterMannequinSales(
        [twintania, omega, odin],
        { ...ANY, datacenter: 'Light', world: 'Odin' },
        WORLDS,
      ),
    ).toEqual([odin]);
  });

  it('waits on the world tree when a location is set', () => {
    expect(
      filterMannequinSales([odin], { ...ANY, datacenter: 'Light' }, undefined),
    ).toBeUndefined();
  });

  it('matches nothing for a location the tree does not know', () => {
    expect(filterMannequinSales([odin], { ...ANY, datacenter: 'Gone' }, WORLDS)).toEqual([]);
  });

  it('filters by quality', () => {
    const hq = row({ hq: true });
    const nq = row({ hq: false });
    expect(filterMannequinSales([hq, nq], { ...ANY, quality: 'hq' }, undefined)).toEqual([hq]);
    expect(filterMannequinSales([hq, nq], { ...ANY, quality: 'nq' }, undefined)).toEqual([nq]);
  });

  it('keeps sales priced at or above the minimum unit price', () => {
    const cheap = row({ unit_price: 999 });
    const exact = row({ unit_price: 1000 });
    const dear = row({ unit_price: 1001 });
    expect(
      filterMannequinSales([cheap, exact, dear], { ...ANY, minUnitPrice: 1000 }, undefined),
    ).toEqual([exact, dear]);
  });

  it('matches the trimmed buyer anywhere in the name, ignoring case', () => {
    const match = row({ buyer_name: 'Some One' });
    const other = row({ buyer_name: 'Nobody' });
    expect(filterMannequinSales([match, other], { ...ANY, buyer: '  me o ' }, undefined)).toEqual([
      match,
    ]);
  });

  it('treats a blank buyer as any', () => {
    const rows = [row({ buyer_name: 'A' }), row({ buyer_name: 'B' })];
    expect(filterMannequinSales(rows, { ...ANY, buyer: '   ' }, undefined)).toEqual(rows);
  });
});

describe('mannequinScope', () => {
  it('gives each filter combination its own scope', () => {
    expect(mannequinScope({ ...ANY })).toBe(mannequinScope(ANY));
    expect(mannequinScope({ ...ANY, world: 'Odin' })).not.toBe(mannequinScope(ANY));
    expect(mannequinScope({ ...ANY, quality: 'hq' })).not.toBe(mannequinScope(ANY));
    expect(mannequinScope({ ...ANY, buyer: 'x' })).not.toBe(mannequinScope(ANY));
  });
});

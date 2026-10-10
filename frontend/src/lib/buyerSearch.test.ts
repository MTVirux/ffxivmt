import { describe, expect, it } from 'vitest';
import type { BuyerSearchRow } from '../api/types';
import { filterByMinUnitPrice } from './buyerSearch';

function row(quantity: number | null, total_price: number | null): BuyerSearchRow {
  return {
    item_id: 1,
    world_id: 85,
    buyer_name: 'Some One',
    sale_time: '2026-10-10T12:00:00+00:00',
    quantity,
    total_price,
  };
}

describe('filterByMinUnitPrice', () => {
  it('keeps every row, including ones without a quantity, when the min is 0', () => {
    const rows = [row(1, 100), row(null, null)];
    expect(filterByMinUnitPrice(rows, 0)).toEqual(rows);
  });

  it('keeps rows at or above the min unit price', () => {
    const atMin = row(2, 1000);
    const above = row(1, 900);
    expect(filterByMinUnitPrice([atMin, above, row(1, 499)], 500)).toEqual([atMin, above]);
  });

  it('compares the unit price, not the total', () => {
    expect(filterByMinUnitPrice([row(10, 1000)], 150)).toEqual([]);
  });

  it('drops rows without a quantity once a min is set', () => {
    expect(filterByMinUnitPrice([row(null, null)], 1)).toEqual([]);
  });
});

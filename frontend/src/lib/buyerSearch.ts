import type { BuyerSearchRow } from '../api/types';

/** Rows without a quantity have no known unit price, so any min above 0 drops them. */
export function filterByMinUnitPrice(
  rows: BuyerSearchRow[],
  minUnitPrice: number,
): BuyerSearchRow[] {
  if (minUnitPrice <= 0) return rows;
  return rows.filter(
    (r) =>
      r.quantity !== null && r.total_price !== null && r.total_price / r.quantity >= minUnitPrice,
  );
}

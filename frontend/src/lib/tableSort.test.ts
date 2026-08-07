import { describe, it, expect } from 'vitest';
import { resolveSort } from './tableSort';

const FALLBACK = [{ id: 'ffmt_score', desc: true }];
const IDS = ['name', 'ffmt_score', 'min_price'];

describe('resolveSort', () => {
  it('uses the fallback when nothing was ever saved', () => {
    expect(resolveSort(undefined, IDS, FALLBACK)).toEqual(FALLBACK);
  });
  it('keeps a deliberately cleared sort', () => {
    expect(resolveSort([], IDS, FALLBACK)).toEqual([]);
  });
  it('restores a saved sort on a column that still exists', () => {
    expect(resolveSort([{ id: 'min_price', desc: false }], IDS, FALLBACK)).toEqual([
      { id: 'min_price', desc: false },
    ]);
  });
  it('falls back when every saved column is gone', () => {
    expect(resolveSort([{ id: '3h', desc: true }], IDS, FALLBACK)).toEqual(FALLBACK);
  });
  it('drops only the missing columns when some survive', () => {
    expect(
      resolveSort([{ id: '3h', desc: true }, { id: 'name', desc: false }], IDS, FALLBACK),
    ).toEqual([{ id: 'name', desc: false }]);
  });
});

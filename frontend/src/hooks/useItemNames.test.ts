import { describe, it, expect } from 'vitest';
import { splitItemNames } from './useItemNames';

const list = { '1': 'Gil', '2': 'Fire Shard' };

describe('splitItemNames', () => {
  it('takes names from the list', () => {
    const { names, missing } = splitItemNames([1, 2], list);
    expect(names).toEqual(
      new Map([
        [1, 'Gil'],
        [2, 'Fire Shard'],
      ]),
    );
    expect(missing).toEqual([]);
  });

  it('routes ids missing from the list to the per-item fallback', () => {
    const { names, missing } = splitItemNames([1, 44000], list);
    expect(names).toEqual(new Map([[1, 'Gil']]));
    expect(missing).toEqual([44000]);
  });

  it('routes every id to the fallback when there is no list', () => {
    const { names, missing } = splitItemNames([1, 2], 'unavailable');
    expect(names.size).toBe(0);
    expect(missing).toEqual([1, 2]);
  });

  it('holds back per-item requests while the list is loading', () => {
    const { names, missing } = splitItemNames([1, 2], 'loading');
    expect(names.size).toBe(0);
    expect(missing).toEqual([]);
  });
});

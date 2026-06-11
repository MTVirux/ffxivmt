import { describe, it, expect } from 'vitest';
import { buildWorldNameMap } from './worlds';

describe('buildWorldNameMap', () => {
  it('flattens the world tree into an id to name map', () => {
    const tree = {
      'North-America': { Aether: { '1': 'Gilgamesh', '2': 'Faerie' } },
      Europe: { Chaos: { '3': 'Ramuh' } },
    };
    const map = buildWorldNameMap(tree);
    expect(map.get(1)).toBe('Gilgamesh');
    expect(map.get(2)).toBe('Faerie');
    expect(map.get(3)).toBe('Ramuh');
    expect(map.size).toBe(3);
  });

  it('returns an empty map for undefined input', () => {
    expect(buildWorldNameMap(undefined).size).toBe(0);
  });
});

import { describe, it, expect } from 'vitest';
import { matchesItemName } from './itemFilter';

describe('matchesItemName', () => {
  it('matches everything when the query is empty', () => {
    expect(matchesItemName('Earth Shard', '')).toBe(true);
  });
  it('matches everything when the query is only whitespace', () => {
    expect(matchesItemName('Earth Shard', '   ')).toBe(true);
  });
  it('is case-insensitive', () => {
    expect(matchesItemName('Earth Shard', 'EARTH')).toBe(true);
  });
  it('matches a substring anywhere in the name', () => {
    expect(matchesItemName('Earthbreak Aethersand', 'sand')).toBe(true);
  });
  it('trims surrounding whitespace in the query', () => {
    expect(matchesItemName('Earth Shard', '  shard ')).toBe(true);
  });
  it('returns false when the query is absent from the name', () => {
    expect(matchesItemName('Earth Shard', 'fire')).toBe(false);
  });
});

import { describe, it, expect } from 'vitest';
import { CURRENCY_GROUPS, filterCurrencyGroups, isKnownCurrency } from './currencies';

describe('CURRENCY_GROUPS', () => {
  it('has a non-empty name and category for every entry', () => {
    for (const group of CURRENCY_GROUPS) {
      expect(group.category).not.toBe('');
      expect(group.currencies.length).toBeGreaterThan(0);
      for (const currency of group.currencies) {
        expect(currency.name).not.toBe('');
      }
    }
  });

  it('has no duplicate item ids across groups', () => {
    const ids = CURRENCY_GROUPS.flatMap((g) => g.currencies.map((c) => c.id));
    expect(new Set(ids).size).toBe(ids.length);
  });
});

describe('isKnownCurrency', () => {
  it('recognises an exact catalogue name', () => {
    expect(isKnownCurrency('Allagan Tomestone of Poetics')).toBe(true);
  });

  it('rejects a partial or unknown name', () => {
    expect(isKnownCurrency('Poetics')).toBe(false);
    expect(isKnownCurrency('')).toBe(false);
  });
});

describe('filterCurrencyGroups', () => {
  it('returns every group for an empty query', () => {
    expect(filterCurrencyGroups(CURRENCY_GROUPS, '  ')).toEqual(CURRENCY_GROUPS);
  });

  it('narrows to the groups holding a matching currency', () => {
    const groups = filterCurrencyGroups(CURRENCY_GROUPS, 'Poetics');
    expect(groups).toHaveLength(1);
    expect(groups[0].category).toBe('Tomestones');
    expect(groups[0].currencies.map((c) => c.name)).toEqual([
      'Allagan Tomestone of Poetics',
    ]);
  });

  it('keeps a whole group when the category matches', () => {
    const groups = filterCurrencyGroups(CURRENCY_GROUPS, 'pvp');
    expect(groups).toHaveLength(1);
    expect(groups[0].currencies.map((c) => c.name)).toEqual([
      'Wolf Mark',
      'Trophy Crystal',
    ]);
  });

  it('matches case-insensitively', () => {
    expect(filterCurrencyGroups(CURRENCY_GROUPS, 'wOlF mArK')).toHaveLength(1);
  });

  it('returns nothing when there is no match', () => {
    expect(filterCurrencyGroups(CURRENCY_GROUPS, 'zzzz')).toEqual([]);
  });
});

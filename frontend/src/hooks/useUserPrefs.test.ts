import { describe, it, expect } from 'vitest';
import { parsePrefs } from './useUserPrefs';

describe('parsePrefs', () => {
  it('returns defaults when null', () => {
    expect(parsePrefs(null)).toEqual({ hiddenTimeframes: [], ignoredItemIds: [] });
  });
  it('returns defaults on corrupt JSON', () => {
    expect(parsePrefs('{')).toEqual({ hiddenTimeframes: [], ignoredItemIds: [] });
  });
  it('fills missing array fields with defaults', () => {
    expect(parsePrefs('{}')).toEqual({ hiddenTimeframes: [], ignoredItemIds: [] });
  });
  it('parses full prefs correctly', () => {
    const input = JSON.stringify({
      hiddenTimeframes: ['1h', '3h'],
      ignoredItemIds: [42, 99],
      lastLocation: { kind: 'world', name: 'Chaos', worldId: 1 },
    });
    expect(parsePrefs(input)).toEqual({
      hiddenTimeframes: ['1h', '3h'],
      ignoredItemIds: [42, 99],
      lastLocation: { kind: 'world', name: 'Chaos', worldId: 1 },
    });
  });
  it('ignores non-array fields', () => {
    expect(parsePrefs('{"hiddenTimeframes":"bad","ignoredItemIds":123}')).toEqual({
      hiddenTimeframes: [],
      ignoredItemIds: [],
    });
  });
  it('preserves lastWorldId', () => {
    expect(parsePrefs('{"lastWorldId":7}')).toEqual({
      hiddenTimeframes: [],
      ignoredItemIds: [],
      lastWorldId: 7,
    });
  });
});

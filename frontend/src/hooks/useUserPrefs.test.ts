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
  it('drops non-positive or non-finite lastWorldId', () => {
    expect(parsePrefs('{"lastWorldId":0}')).toEqual({ hiddenTimeframes: [], ignoredItemIds: [] });
    expect(parsePrefs('{"lastWorldId":-1}')).toEqual({ hiddenTimeframes: [], ignoredItemIds: [] });
    expect(parsePrefs('{"lastWorldId":null}')).toEqual({ hiddenTimeframes: [], ignoredItemIds: [] });
  });
  it('filters non-string entries from hiddenTimeframes', () => {
    expect(parsePrefs('{"hiddenTimeframes":["1h",42,null,"3h"]}')).toEqual({
      hiddenTimeframes: ['1h', '3h'],
      ignoredItemIds: [],
    });
  });
  it('filters non-number entries from ignoredItemIds', () => {
    expect(parsePrefs('{"ignoredItemIds":["foo",42,null,99]}')).toEqual({
      hiddenTimeframes: [],
      ignoredItemIds: [42, 99],
    });
  });
  it('drops invalid lastLocation', () => {
    expect(parsePrefs('{"lastLocation":"bad"}')).toEqual({
      hiddenTimeframes: [],
      ignoredItemIds: [],
    });
  });
});

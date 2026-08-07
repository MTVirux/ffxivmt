import { describe, it, expect } from 'vitest';
import { parsePrefs, type UserPrefs } from './useUserPrefs';

const DEFAULTS: UserPrefs = {
  hiddenTimeframes: [],
  ignoredItemIds: [],
  showHidden: false,
  craftedOnly: false,
  tableSort: {},
  toolInputs: { currencyEff: '', itemProfit: '', buyerSearch: '' },
  buyerSearchWorld: '',
};

describe('parsePrefs', () => {
  it('returns defaults when null', () => {
    expect(parsePrefs(null)).toEqual(DEFAULTS);
  });
  it('returns defaults on corrupt JSON', () => {
    expect(parsePrefs('{')).toEqual(DEFAULTS);
  });
  it('returns defaults on a non-object payload', () => {
    expect(parsePrefs('"nope"')).toEqual(DEFAULTS);
    expect(parsePrefs('42')).toEqual(DEFAULTS);
  });
  it('fills missing fields with defaults', () => {
    expect(parsePrefs('{}')).toEqual(DEFAULTS);
  });
  it('parses full prefs correctly', () => {
    const input = JSON.stringify({
      hiddenTimeframes: ['1h', '3h'],
      ignoredItemIds: [42, 99],
      lastLocation: { kind: 'world', name: 'Chaos', worldId: 1 },
      showHidden: true,
      craftedOnly: true,
      tableSort: { ranking: [{ id: '6h', desc: false }] },
      toolInputs: { currencyEff: 'Poetics', itemProfit: 'Mythril Ingot', buyerSearch: 'Some One' },
      buyerSearchWorld: 'Cerberus',
    });
    expect(parsePrefs(input)).toEqual({
      hiddenTimeframes: ['1h', '3h'],
      ignoredItemIds: [42, 99],
      lastLocation: { kind: 'world', name: 'Chaos', worldId: 1 },
      showHidden: true,
      craftedOnly: true,
      tableSort: { ranking: [{ id: '6h', desc: false }] },
      toolInputs: { currencyEff: 'Poetics', itemProfit: 'Mythril Ingot', buyerSearch: 'Some One' },
      buyerSearchWorld: 'Cerberus',
    });
  });
  it('ignores non-array fields', () => {
    expect(parsePrefs('{"hiddenTimeframes":"bad","ignoredItemIds":123}')).toEqual(DEFAULTS);
  });
  it('filters non-string entries from hiddenTimeframes', () => {
    expect(parsePrefs('{"hiddenTimeframes":["1h",42,null,"3h"]}')).toEqual({
      ...DEFAULTS,
      hiddenTimeframes: ['1h', '3h'],
    });
  });
  it('filters non-number entries from ignoredItemIds', () => {
    expect(parsePrefs('{"ignoredItemIds":["foo",42,null,99]}')).toEqual({
      ...DEFAULTS,
      ignoredItemIds: [42, 99],
    });
  });
  it('drops invalid lastLocation', () => {
    expect(parsePrefs('{"lastLocation":"bad"}')).toEqual(DEFAULTS);
    expect(parsePrefs('{"lastLocation":{"kind":"galaxy","name":"x"}}')).toEqual(DEFAULTS);
  });
  it('still parses prefs written before the new fields existed', () => {
    const legacy = JSON.stringify({
      hiddenTimeframes: ['12h'],
      ignoredItemIds: [7],
      lastLocation: { kind: 'datacenter', name: 'Light' },
      lastWorldId: 9,
    });
    expect(parsePrefs(legacy)).toEqual({
      ...DEFAULTS,
      hiddenTimeframes: ['12h'],
      ignoredItemIds: [7],
      lastLocation: { kind: 'datacenter', name: 'Light' },
    });
  });
  it('falls back per field rather than discarding siblings', () => {
    expect(parsePrefs('{"showHidden":"yes","craftedOnly":true,"buyerSearchWorld":"Phoenix"}')).toEqual({
      ...DEFAULTS,
      craftedOnly: true,
      buyerSearchWorld: 'Phoenix',
    });
  });
  it('drops malformed sort entries but keeps valid ones', () => {
    expect(
      parsePrefs('{"tableSort":{"ranking":[{"id":"1h","desc":true},{"id":5},null],"itemProfit":"bad"}}'),
    ).toEqual({
      ...DEFAULTS,
      tableSort: { ranking: [{ id: '1h', desc: true }], itemProfit: [] },
    });
  });
  it('keeps an empty sort distinct from an absent one', () => {
    const parsed = parsePrefs('{"tableSort":{"ranking":[]}}');
    expect(parsed.tableSort.ranking).toEqual([]);
    expect(parsed.tableSort.currencyEff).toBeUndefined();
  });
});

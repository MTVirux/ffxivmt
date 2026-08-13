import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
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

// Fake window so the store can be exercised without a DOM environment.
function installWindow() {
  const storage = new Map<string, string>();
  const handlers = new Map<string, Set<(event: unknown) => void>>();
  const fake = {
    localStorage: {
      getItem: (key: string) => storage.get(key) ?? null,
      setItem: (key: string, value: string) => void storage.set(key, value),
      removeItem: (key: string) => void storage.delete(key),
    },
    addEventListener: (type: string, handler: (event: unknown) => void) => {
      const set = handlers.get(type) ?? new Set();
      set.add(handler);
      handlers.set(type, set);
    },
    removeEventListener: (type: string, handler: (event: unknown) => void) => {
      handlers.get(type)?.delete(handler);
    },
  };
  (globalThis as unknown as { window: unknown }).window = fake;
  return {
    storage,
    listenerCount: (type: string) => handlers.get(type)?.size ?? 0,
    fire: (type: string, event: unknown) => handlers.get(type)?.forEach((h) => h(event)),
  };
}

describe('prefs store', () => {
  let win: ReturnType<typeof installWindow>;

  beforeEach(() => {
    win = installWindow();
  });

  afterEach(() => {
    delete (globalThis as unknown as { window?: unknown }).window;
  });

  // Fresh module registry per test so the module-level store starts empty.
  async function freshStore() {
    vi.resetModules();
    return import('./useUserPrefs');
  }

  it('shares writes between two independent readers', async () => {
    const { getPrefsSnapshot, subscribePrefs, patchPrefs } = await freshStore();
    let a = 0;
    let b = 0;
    const unsubA = subscribePrefs(() => void a++);
    const unsubB = subscribePrefs(() => void b++);

    patchPrefs({ hiddenTimeframes: ['1h'] });
    patchPrefs((prev) => ({
      tableSort: { ...prev.tableSort, ranking: [{ id: '6h', desc: true }] },
    }));

    expect(a).toBe(2);
    expect(b).toBe(2);
    expect(getPrefsSnapshot().hiddenTimeframes).toEqual(['1h']);
    expect(getPrefsSnapshot().tableSort.ranking).toEqual([{ id: '6h', desc: true }]);

    unsubA();
    unsubB();
  });

  it('persists the merged object so a later write cannot clobber an earlier key', async () => {
    const { patchPrefs } = await freshStore();
    patchPrefs({ hiddenTimeframes: ['1h'] });
    patchPrefs({ showHidden: true });

    expect(JSON.parse(win.storage.get('ffmt:prefs') ?? '{}')).toMatchObject({
      hiddenTimeframes: ['1h'],
      showHidden: true,
    });
  });

  it('returns a stable snapshot until a patch lands', async () => {
    const { getPrefsSnapshot, patchPrefs } = await freshStore();
    const first = getPrefsSnapshot();
    expect(getPrefsSnapshot()).toBe(first);

    patchPrefs({ showHidden: true });
    expect(getPrefsSnapshot()).not.toBe(first);
  });

  it('seeds the snapshot from localStorage', async () => {
    win.storage.set('ffmt:prefs', JSON.stringify({ buyerSearchWorld: 'Phoenix' }));
    const { getPrefsSnapshot } = await freshStore();
    expect(getPrefsSnapshot().buyerSearchWorld).toBe('Phoenix');
  });

  it('applies cross-tab storage events to every reader', async () => {
    const { getPrefsSnapshot, subscribePrefs } = await freshStore();
    let notified = 0;
    const unsub = subscribePrefs(() => void notified++);

    win.fire('storage', { key: 'ffmt:prefs', newValue: JSON.stringify({ showHidden: true }) });

    expect(notified).toBe(1);
    expect(getPrefsSnapshot().showHidden).toBe(true);
    unsub();
  });

  it('binds the storage listener only while someone is subscribed', async () => {
    const { subscribePrefs } = await freshStore();
    expect(win.listenerCount('storage')).toBe(0);
    const unsubA = subscribePrefs(() => {});
    const unsubB = subscribePrefs(() => {});
    expect(win.listenerCount('storage')).toBe(1);
    unsubA();
    expect(win.listenerCount('storage')).toBe(1);
    unsubB();
    expect(win.listenerCount('storage')).toBe(0);
  });
});

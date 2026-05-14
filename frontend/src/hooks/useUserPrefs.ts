import { useCallback, useEffect, useState } from 'react';
import type { LocationKind } from '../api/types';

export type UserPrefs = {
  lastLocation?: { kind: LocationKind; name: string; worldId?: number };
  lastWorldId?: number;
  hiddenTimeframes: string[];
  ignoredItemIds: number[];
};

const KEY = 'ffmt:prefs';
const DEFAULTS: UserPrefs = { hiddenTimeframes: [], ignoredItemIds: [] };

export function parsePrefs(raw: string | null): UserPrefs {
  if (!raw) return { ...DEFAULTS };
  try {
    const parsed = JSON.parse(raw) as Partial<UserPrefs>;

    const loc = parsed.lastLocation as unknown;
    const validKinds = ['world', 'datacenter', 'region'];
    let lastLocation: UserPrefs['lastLocation'] | undefined;
    if (
      loc !== null &&
      typeof loc === 'object' &&
      'name' in (loc as object) &&
      'kind' in (loc as object) &&
      typeof (loc as { name: unknown }).name === 'string' &&
      validKinds.includes((loc as { kind: unknown }).kind as string)
    ) {
      lastLocation = loc as UserPrefs['lastLocation'];
    }

    return {
      hiddenTimeframes: Array.isArray(parsed.hiddenTimeframes)
        ? parsed.hiddenTimeframes.filter((x): x is string => typeof x === 'string')
        : [],
      ignoredItemIds: Array.isArray(parsed.ignoredItemIds)
        ? parsed.ignoredItemIds.filter((x): x is number => typeof x === 'number')
        : [],
      ...(lastLocation ? { lastLocation } : {}),
      ...(typeof parsed.lastWorldId === 'number' && Number.isFinite(parsed.lastWorldId) && parsed.lastWorldId > 0
        ? { lastWorldId: parsed.lastWorldId }
        : {}),
    };
  } catch {
    return { ...DEFAULTS };
  }
}

type PatchArg = Partial<UserPrefs> | ((prev: UserPrefs) => Partial<UserPrefs>);

export function useUserPrefs(): [UserPrefs, (patch: PatchArg) => void] {
  const [prefs, setPrefs] = useState<UserPrefs>(() =>
    typeof window === 'undefined' ? DEFAULTS : parsePrefs(window.localStorage.getItem(KEY)),
  );

  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === KEY) setPrefs(parsePrefs(e.newValue));
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const patchPrefs = useCallback((patch: PatchArg) => {
    setPrefs((prev) => {
      const partial = typeof patch === 'function' ? patch(prev) : patch;
      const next = { ...prev, ...partial };
      if (typeof window !== 'undefined') {
        try {
          localStorage.setItem(KEY, JSON.stringify(next));
        } catch {}
      }
      return next;
    });
  }, []);

  return [prefs, patchPrefs];
}

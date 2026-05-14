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
  if (!raw) return DEFAULTS;
  try {
    const parsed = JSON.parse(raw) as Partial<UserPrefs>;
    return {
      hiddenTimeframes: Array.isArray(parsed.hiddenTimeframes) ? parsed.hiddenTimeframes : [],
      ignoredItemIds: Array.isArray(parsed.ignoredItemIds) ? parsed.ignoredItemIds : [],
      ...(parsed.lastLocation ? { lastLocation: parsed.lastLocation } : {}),
      ...(typeof parsed.lastWorldId === 'number' ? { lastWorldId: parsed.lastWorldId } : {}),
    };
  } catch {
    return DEFAULTS;
  }
}

export function useUserPrefs(): [UserPrefs, (patch: Partial<UserPrefs>) => void] {
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

  const patch = useCallback((update: Partial<UserPrefs>) => {
    setPrefs((prev) => {
      const next = { ...prev, ...update };
      window.localStorage.setItem(KEY, JSON.stringify(next));
      return next;
    });
  }, []);

  return [prefs, patch];
}

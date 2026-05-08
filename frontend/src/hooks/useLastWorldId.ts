import { useCallback, useEffect, useState } from 'react';

const KEY = 'ffmt:lastWorldId';

function read(): number | undefined {
  if (typeof window === 'undefined') return undefined;
  const raw = window.localStorage.getItem(KEY);
  if (!raw) return undefined;
  const n = Number(raw);
  return Number.isFinite(n) && n > 0 ? n : undefined;
}

export function useLastWorldId(): [number | undefined, (id: number | undefined) => void] {
  const [worldId, setWorldId] = useState<number | undefined>(read);

  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === KEY) setWorldId(read());
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const set = useCallback((id: number | undefined) => {
    if (id === undefined) {
      window.localStorage.removeItem(KEY);
    } else {
      window.localStorage.setItem(KEY, String(id));
    }
    setWorldId(id);
  }, []);

  return [worldId, set];
}

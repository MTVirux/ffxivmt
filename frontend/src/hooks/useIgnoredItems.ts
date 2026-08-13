import { useMemo } from 'react';
import { patchPrefs, useUserPrefs } from './useUserPrefs';

function ignore(id: number) {
  patchPrefs((prev) => ({ ignoredItemIds: [...prev.ignoredItemIds, id] }));
}

function unignore(id: number) {
  patchPrefs((prev) => ({ ignoredItemIds: prev.ignoredItemIds.filter((x) => x !== id) }));
}

function toggle(id: number) {
  patchPrefs((prev) => ({
    ignoredItemIds: prev.ignoredItemIds.includes(id)
      ? prev.ignoredItemIds.filter((x) => x !== id)
      : [...prev.ignoredItemIds, id],
  }));
}

// Stable handler identities keep virtualized tables from rebuilding their columns
// and re-deriving their sort on every render.
export function useIgnoredItems() {
  const [prefs] = useUserPrefs();
  const ids = prefs.ignoredItemIds;

  return useMemo(
    () => ({
      ids,
      ignore,
      unignore,
      toggle,
      isIgnored: (id: number) => ids.includes(id),
    }),
    [ids],
  );
}

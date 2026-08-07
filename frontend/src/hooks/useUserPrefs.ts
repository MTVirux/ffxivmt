import { useCallback, useEffect, useState } from 'react';
import { z } from 'zod';

const locationSchema = z.object({
  kind: z.enum(['world', 'datacenter', 'region']),
  name: z.string(),
  worldId: z.number().finite().positive().optional(),
});

const sortEntrySchema = z.object({ id: z.string(), desc: z.boolean() });

// Drops junk elements but keeps the good ones; plain z.array rejects the whole array.
function lenientArray<T>(element: z.ZodType<T>) {
  return z
    .array(z.unknown())
    .catch([])
    .transform((items) =>
      items.flatMap((item) => {
        const parsed = element.safeParse(item);
        return parsed.success ? [parsed.data] : [];
      }),
    );
}

const prefsSchema = z.object({
  lastLocation: locationSchema.optional().catch(undefined),
  hiddenTimeframes: lenientArray(z.string()),
  ignoredItemIds: lenientArray(z.number().finite()),
  showHidden: z.boolean().catch(false),
  craftedOnly: z.boolean().catch(false),
  tableSort: z
    .object({
      ranking: lenientArray(sortEntrySchema).optional().catch(undefined),
      currencyEff: lenientArray(sortEntrySchema).optional().catch(undefined),
      itemProfit: lenientArray(sortEntrySchema).optional().catch(undefined),
    })
    .catch({}),
  toolInputs: z
    .object({
      currencyEff: z.string().catch(''),
      itemProfit: z.string().catch(''),
      buyerSearch: z.string().catch(''),
    })
    .catch({ currencyEff: '', itemProfit: '', buyerSearch: '' }),
  buyerSearchWorld: z.string().catch(''),
});

export type UserPrefs = z.infer<typeof prefsSchema>;
export type TableSortKey = keyof UserPrefs['tableSort'];

const KEY = 'ffmt:prefs';

function defaults(): UserPrefs {
  return {
    hiddenTimeframes: [],
    ignoredItemIds: [],
    showHidden: false,
    craftedOnly: false,
    tableSort: {},
    toolInputs: { currencyEff: '', itemProfit: '', buyerSearch: '' },
    buyerSearchWorld: '',
  };
}

export function parsePrefs(raw: string | null): UserPrefs {
  if (!raw) return defaults();
  try {
    return prefsSchema.parse(JSON.parse(raw));
  } catch {
    return defaults();
  }
}

type PatchArg = Partial<UserPrefs> | ((prev: UserPrefs) => Partial<UserPrefs>);

export function useUserPrefs(): [UserPrefs, (patch: PatchArg) => void] {
  const [prefs, setPrefs] = useState<UserPrefs>(() =>
    typeof window === 'undefined' ? defaults() : parsePrefs(window.localStorage.getItem(KEY)),
  );

  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === KEY) setPrefs(parsePrefs(e.newValue));
    };
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  // Shallow merge — patches touching tableSort or toolInputs must spread the previous nested object.
  const patchPrefs = useCallback((patch: PatchArg) => {
    setPrefs((prev) => {
      const partial = typeof patch === 'function' ? patch(prev) : patch;
      const next = { ...prev, ...partial };
      if (typeof window !== 'undefined') {
        try {
          localStorage.setItem(KEY, JSON.stringify(next));
        } catch {
          // quota exceeded or storage blocked - prefs stay in-memory for this session
        }
      }
      return next;
    });
  }, []);

  return [prefs, patchPrefs];
}

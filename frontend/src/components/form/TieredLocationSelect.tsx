import { useEffect, useId, useMemo, type ReactNode } from 'react';
import { useWorlds } from '../../hooks/useWorlds';
import type { Location } from '../../api/types';

type Props = {
  value: Location | undefined;
  onChange: (next: Location) => void;
  className?: string;
};

// Cascading Region · DC · World; the deepest non-empty pick wins.
export default function TieredLocationSelect({ value, onChange, className }: Props) {
  const { data } = useWorlds();
  const ids = {
    region: useId(),
    dc: useId(),
    world: useId(),
  };

  const trio = useMemo(() => deriveTrio(value, data), [value, data]);

  // Seed the first region so the page renders something before the user picks.
  useEffect(() => {
    if (data && !value) {
      const firstRegion = Object.keys(data)[0];
      if (firstRegion) onChange({ kind: 'region', name: firstRegion });
    }
  }, [data, value, onChange]);

  const regions = useMemo(() => (data ? Object.keys(data) : []), [data]);
  const dcs = useMemo(() => {
    if (!data || !trio.region) return [];
    return Object.keys(data[trio.region] ?? {});
  }, [data, trio.region]);
  const worlds = useMemo(() => {
    if (!data || !trio.region || !trio.dc) return [];
    return Object.entries(data[trio.region]?.[trio.dc] ?? {})
      .map(([id, name]) => ({ id: Number(id), name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [data, trio.region, trio.dc]);

  return (
    <div className={['flex flex-wrap items-end gap-3', className].filter(Boolean).join(' ')}>
      <Field id={ids.region} label="Region">
        <select
          id={ids.region}
          value={trio.region ?? ''}
          disabled={!data}
          onChange={(e) => {
            const region = e.target.value;
            onChange({ kind: 'region', name: region });
          }}
          className={selectClasses}
        >
          {regions.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
      </Field>

      <Field id={ids.dc} label="Datacenter">
        <select
          id={ids.dc}
          value={trio.dc ?? ''}
          disabled={!trio.region || dcs.length === 0}
          onChange={(e) => {
            const dc = e.target.value;
            if (!trio.region) return;
            if (dc === '') onChange({ kind: 'region', name: trio.region });
            else onChange({ kind: 'datacenter', name: dc });
          }}
          className={selectClasses}
        >
          <option value="">All DCs</option>
          {dcs.map((dc) => (
            <option key={dc} value={dc}>
              {dc}
            </option>
          ))}
        </select>
      </Field>

      <Field id={ids.world} label="World">
        <select
          id={ids.world}
          value={trio.worldId ?? ''}
          disabled={!trio.dc || worlds.length === 0}
          onChange={(e) => {
            const raw = e.target.value;
            if (raw === '') {
              if (trio.dc) onChange({ kind: 'datacenter', name: trio.dc });
              return;
            }
            const wid = Number(raw);
            const w = worlds.find((x) => x.id === wid);
            if (w) onChange({ kind: 'world', name: w.name, worldId: w.id });
          }}
          className={selectClasses}
        >
          <option value="">All worlds</option>
          {worlds.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </select>
      </Field>
    </div>
  );
}

const selectClasses =
  'rounded-md border border-border/60 bg-card px-3 py-2 text-sm text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50 disabled:cursor-not-allowed disabled:opacity-50';

function Field({
  id,
  label,
  children,
}: {
  id: string;
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-xs uppercase tracking-widest text-muted-foreground">
        {label}
      </label>
      {children}
    </div>
  );
}

type Trio = { region?: string; dc?: string; worldId?: number };

// Recovers (region, dc, world) by walking the world tree — needed because a
// Location only carries the deepest pick's name (e.g. "Light" with no region).
function deriveTrio(
  value: Location | undefined,
  tree: Record<string, Record<string, Record<string, string>>> | undefined,
): Trio {
  if (!value || !tree) return {};

  if (value.kind === 'region') {
    return { region: tree[value.name] ? value.name : Object.keys(tree)[0] };
  }

  if (value.kind === 'datacenter') {
    for (const [region, dcs] of Object.entries(tree)) {
      if (dcs[value.name]) return { region, dc: value.name };
    }
    return {};
  }

  for (const [region, dcs] of Object.entries(tree)) {
    for (const [dc, worlds] of Object.entries(dcs)) {
      for (const [wid, wname] of Object.entries(worlds)) {
        if (
          (value.worldId !== undefined && Number(wid) === value.worldId) ||
          wname === value.name
        ) {
          return { region, dc, worldId: Number(wid) };
        }
      }
    }
  }
  return {};
}

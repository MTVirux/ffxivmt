import { useMemo } from 'react';
import { useWorlds } from '../../hooks/useWorlds';

type Props = {
  worldId: number | undefined;
  onChange: (worldId: number) => void;
  id?: string;
  className?: string;
};

export default function LocationSelect({ worldId, onChange, id, className }: Props) {
  const worlds = useWorlds();

  const groups = useMemo(() => {
    if (!worlds.data) return [] as { label: string; options: { id: number; name: string }[] }[];
    const out: { label: string; options: { id: number; name: string }[] }[] = [];
    for (const [region, dcs] of Object.entries(worlds.data)) {
      for (const [dc, ws] of Object.entries(dcs)) {
        out.push({
          label: `${region} · ${dc}`,
          options: Object.entries(ws)
            .map(([wid, name]) => ({ id: Number(wid), name }))
            .sort((a, b) => a.name.localeCompare(b.name)),
        });
      }
    }
    return out;
  }, [worlds.data]);

  return (
    <select
      id={id}
      value={worldId ?? ''}
      disabled={worlds.isLoading || !worlds.data}
      onChange={(e) => {
        const next = Number(e.target.value);
        if (Number.isFinite(next) && next > 0) onChange(next);
      }}
      className={[
        'rounded-md border border-border/60 bg-card px-3 py-2 text-sm',
        'text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50',
        'disabled:cursor-not-allowed disabled:opacity-50',
        className,
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {worldId === undefined && <option value="">Pick a world…</option>}
      {groups.map((g) => (
        <optgroup key={g.label} label={g.label}>
          {g.options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.name}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  );
}

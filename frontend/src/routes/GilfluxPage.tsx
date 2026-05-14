import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import RankingTable from '../components/data/RankingTable';
import TieredLocationSelect from '../components/form/TieredLocationSelect';
import { useGilfluxRanking } from '../hooks/useGilfluxRanking';
import { aggregateRankings } from '../lib/rankingAggregate';
import { formatNumber } from '../lib/format';
import { useAppConfig } from '../hooks/useAppConfig';
import { useUserPrefs } from '../hooks/useUserPrefs';
import type { Location, LocationKind } from '../api/types';

export default function GilfluxPage() {
  const [prefs, patchPrefs] = useUserPrefs();
  const [showHidden, setShowHidden] = useState(false);
  const [searchParams, setSearchParams] = useSearchParams();

  const location = useMemo<Location | undefined>(() => {
    const name = searchParams.get('loc');
    const kind = searchParams.get('kind') as LocationKind | null;
    if (!name || !kind) return prefs.lastLocation;
    const wid = searchParams.get('wid');
    return { kind, name, ...(kind === 'world' && wid ? { worldId: Number(wid) } : {}) };
  }, [searchParams, prefs.lastLocation]);

  const craftedOnly = searchParams.get('crafted') === '1';

  const setLocation = (next: Location) => {
    patchPrefs({ lastLocation: next });
    setSearchParams(
      (prev) => {
        prev.set('loc', next.name);
        prev.set('kind', next.kind);
        if (next.kind === 'world' && next.worldId !== undefined) prev.set('wid', String(next.worldId));
        else prev.delete('wid');
        return prev;
      },
      { replace: true },
    );
  };

  const setCraftedOnly = (next: boolean) => {
    setSearchParams(
      (prev) => {
        if (next) prev.set('crafted', '1');
        else prev.delete('crafted');
        return prev;
      },
      { replace: true },
    );
  };

  const config = useAppConfig();
  const timeframes = config.gilflux_timeframes.map((key) => ({ key, label: key }));

  const visibleTimeframes = timeframes.filter((tf) => !prefs.hiddenTimeframes.includes(tf.key));

  const toggleTimeframe = (key: string) => {
    const isHidden = prefs.hiddenTimeframes.includes(key);
    if (!isHidden && visibleTimeframes.length === 1) return;
    patchPrefs({
      hiddenTimeframes: isHidden
        ? prefs.hiddenTimeframes.filter((k) => k !== key)
        : [...prefs.hiddenTimeframes, key],
    });
  };

  const query = useGilfluxRanking(location, craftedOnly);
  const rows = useMemo(() => aggregateRankings(query.data ?? []), [query.data]);
  const visibleRows = showHidden
    ? rows
    : rows.filter((r) => !prefs.ignoredItemIds.includes(r.item_id));
  const showWorldExpand = location?.kind !== 'world';

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">gilflux</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Top movers</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Items ranked by total gil moved over each timeframe. Pick a region, narrow to a
          datacenter, or zoom into a single world.
        </p>
      </header>

      <div className="flex flex-wrap items-end justify-between gap-6 rounded-xl border border-border/60 bg-card/40 p-4">
        <TieredLocationSelect value={location} onChange={setLocation} />
        <CraftedToggle checked={craftedOnly} onChange={setCraftedOnly} />
        <ShowHiddenToggle checked={showHidden} onChange={setShowHidden} />
      </div>

      <TimeframeToggles
        timeframes={timeframes}
        hiddenTimeframes={prefs.hiddenTimeframes}
        onToggle={toggleTimeframe}
      />

      <section className="space-y-3">
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <Subtitle location={location} />
          <RowCount loading={query.isLoading} count={visibleRows.length} />
        </div>

        {query.isLoading ? (
          <div className="h-64 animate-pulse rounded-xl bg-card/40" />
        ) : query.isError ? (
          <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
            Failed to load rankings.
          </div>
        ) : (
          <RankingTable
            rows={visibleRows}
            showWorldExpand={showWorldExpand}
            timeframes={visibleTimeframes}
            ignoredItemIds={showHidden ? prefs.ignoredItemIds : undefined}
            onIgnore={(id) => patchPrefs((prev) => ({ ignoredItemIds: [...prev.ignoredItemIds, id] }))}
            onUnignore={
              showHidden
                ? (id) => patchPrefs((prev) => ({ ignoredItemIds: prev.ignoredItemIds.filter((x) => x !== id) }))
                : undefined
            }
          />
        )}
      </section>
    </div>
  );
}

function CraftedToggle({
  checked,
  onChange,
}: {
  checked: boolean;
  onChange: (next: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground hover:text-foreground">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="size-4 rounded border-border/60 bg-card accent-[var(--color-accent)]"
      />
      Crafted items only
    </label>
  );
}

function ShowHiddenToggle({
  checked,
  onChange,
}: {
  checked: boolean;
  onChange: (next: boolean) => void;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground hover:text-foreground">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="size-4 rounded border-border/60 bg-card accent-[var(--color-accent)]"
      />
      Show hidden items
    </label>
  );
}

function Subtitle({ location }: { location: Location | undefined }) {
  if (!location) return <span>—</span>;
  const tag =
    location.kind === 'region'
      ? 'region'
      : location.kind === 'datacenter'
        ? 'datacenter'
        : 'world';
  return (
    <span>
      <span className="uppercase tracking-widest">{tag}</span>{' '}
      <span className="font-mono text-foreground">{location.name}</span>
      {location.kind !== 'world' && (
        <span className="ml-2 text-muted-foreground">· click ▸ to expand worlds</span>
      )}
    </span>
  );
}

function RowCount({ loading, count }: { loading: boolean; count: number }) {
  if (loading) return <span>loading…</span>;
  return <span>{formatNumber(count)} items</span>;
}

function TimeframeToggles({
  timeframes,
  hiddenTimeframes,
  onToggle,
}: {
  timeframes: readonly { key: string; label: string }[];
  hiddenTimeframes: string[];
  onToggle: (key: string) => void;
}) {
  return (
    <div className="flex flex-wrap gap-1">
      {timeframes.map((tf) => {
        const hidden = hiddenTimeframes.includes(tf.key);
        return (
          <button
            key={tf.key}
            type="button"
            onClick={() => onToggle(tf.key)}
            className={[
              'rounded px-2 py-0.5 font-mono text-xs transition-colors',
              hidden
                ? 'bg-card/40 text-muted-foreground hover:text-foreground'
                : 'bg-accent/20 text-accent hover:bg-accent/30',
            ].join(' ')}
          >
            {tf.label}
          </button>
        );
      })}
    </div>
  );
}

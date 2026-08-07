import { useMemo } from 'react';
import { useSearchParams } from 'react-router';
import RankingTable from '../components/data/RankingTable';
import CheckboxToggle from '../components/form/CheckboxToggle';
import TieredLocationSelect from '../components/form/TieredLocationSelect';
import QueryBoundary from '../components/layout/QueryBoundary';
import { useGilfluxRanking } from '../hooks/useGilfluxRanking';
import { aggregateRankings } from '../lib/rankingAggregate';
import { formatNumber } from '../lib/format';
import { useAppConfig } from '../hooks/useAppConfig';
import { useIgnoredItems } from '../hooks/useIgnoredItems';
import { useUserPrefs } from '../hooks/useUserPrefs';
import type { Location, LocationKind } from '../api/types';

export default function GilfluxPage() {
  const [prefs, patchPrefs] = useUserPrefs();
  const { ids: ignoredItemIds, ignore, unignore } = useIgnoredItems();
  const [searchParams, setSearchParams] = useSearchParams();
  const showHidden = prefs.showHidden;
  const setShowHidden = (next: boolean) => patchPrefs({ showHidden: next });

  const location = useMemo<Location | undefined>(() => {
    const name = searchParams.get('loc');
    const kind = searchParams.get('kind') as LocationKind | null;
    if (!name || !kind) return prefs.lastLocation;
    const wid = searchParams.get('wid');
    return { kind, name, ...(kind === 'world' && wid ? { worldId: Number(wid) } : {}) };
  }, [searchParams, prefs.lastLocation]);

  // A shared link wins for this visit; absent the param, fall back to what was saved.
  const craftedParam = searchParams.get('crafted');
  const craftedOnly = craftedParam === null ? prefs.craftedOnly : craftedParam === '1';

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
    patchPrefs({ craftedOnly: next });
    setSearchParams(
      (prev) => {
        prev.set('crafted', next ? '1' : '0');
        return prev;
      },
      { replace: true },
    );
  };

  const config = useAppConfig();
  const timeframes = useMemo(
    () => config.gilflux_timeframes.map((key) => ({ key, label: key })),
    [config.gilflux_timeframes],
  );

  const visibleTimeframes = useMemo(
    () => timeframes.filter((tf) => !prefs.hiddenTimeframes.includes(tf.key)),
    [timeframes, prefs.hiddenTimeframes],
  );

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
  const visibleRows = useMemo(
    () => (showHidden ? rows : rows.filter((r) => !ignoredItemIds.includes(r.item_id))),
    [showHidden, rows, ignoredItemIds],
  );
  const showWorldExpand = location?.kind !== 'world';

  const table = () => (
    <RankingTable
      rows={visibleRows}
      showWorldExpand={showWorldExpand}
      timeframes={visibleTimeframes}
      ignoredItemIds={showHidden ? ignoredItemIds : undefined}
      onIgnore={ignore}
      onUnignore={showHidden ? unignore : undefined}
    />
  );

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">gilflux</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Top movers</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Items ranked by total gil moved over each timeframe.
        </p>
      </header>

      <div className="flex flex-wrap items-end justify-between gap-6 rounded-xl border border-border/60 bg-card/40 p-4">
        <TieredLocationSelect value={location} onChange={setLocation} />
        <CheckboxToggle
          label="Crafted items only"
          checked={craftedOnly}
          onChange={setCraftedOnly}
        />
        <CheckboxToggle label="Show hidden items" checked={showHidden} onChange={setShowHidden} />
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

        {/* The query stays disabled until a location exists - show the table's empty state, not nothing. */}
        {location === undefined ? (
          table()
        ) : (
          <QueryBoundary query={query} errorText="Failed to load rankings.">
            {table}
          </QueryBoundary>
        )}
      </section>
    </div>
  );
}

function Subtitle({ location }: { location: Location | undefined }) {
  if (!location) return <span>—</span>;
  const tag = location.kind;
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

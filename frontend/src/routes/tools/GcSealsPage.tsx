import { useCallback, useMemo, useState, type FormEvent, type KeyboardEvent } from 'react';
import GcSealsBreakdownView from '../../components/data/GcSealsBreakdown';
import GcSealsTable from '../../components/data/GcSealsTable';
import CheckboxToggle from '../../components/form/CheckboxToggle';
import TieredLocationSelect from '../../components/form/TieredLocationSelect';
import EmptyState from '../../components/layout/EmptyState';
import { QUERY_SKELETON_CLASS } from '../../components/layout/QueryBoundary';
import { useGcSealsCatalogue } from '../../hooks/useGcSealsCatalogue';
import { useIgnoredItems } from '../../hooks/useIgnoredItems';
import { useMarketListings, type ListingsTarget } from '../../hooks/useMarketListings';
import { patchPrefs, useUserPrefs } from '../../hooks/useUserPrefs';
import { useWorlds } from '../../hooks/useWorlds';
import {
  boardItemIds,
  buildBreakdown,
  computeRows,
  createPlanner,
  MAX_GC_QUANTITY,
  MAX_SEAL_TARGET,
  parseBoundedInt,
  type GcSealsMode,
} from '../../lib/gcSeals';
import { buildWorldNameMap } from '../../lib/worlds';
import type { Location } from '../../api/types';

type ModeKind = GcSealsMode['kind'];

const LABEL_CLASS = 'text-xs uppercase tracking-widest text-muted-foreground';
const INPUT_CLASS =
  'w-32 rounded-md border border-border/60 bg-card px-3 py-2 text-sm text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50';
const NOTICE_CLASS =
  'rounded-lg border border-border/60 bg-card/40 p-4 text-sm text-muted-foreground';
const ERROR_CLASS = 'rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive';

export default function GcSealsPage() {
  const [prefs] = useUserPrefs();
  const { mode, quantity, sealTarget } = prefs.gcSeals;
  const location = prefs.lastLocation;
  const setLocation = useCallback((next: Location) => patchPrefs({ lastLocation: next }), []);
  const showHidden = prefs.showHidden;
  const { ids: ignoredItemIds, ignore, unignore } = useIgnoredItems();

  const [target, setTarget] = useState<ListingsTarget | null>(null);
  const [locationError, setLocationError] = useState<string | null>(null);
  const [amountDraft, setAmountDraft] = useState(
    String(mode === 'quantity' ? quantity : sealTarget),
  );

  const catalogue = useGcSealsCatalogue();
  const worlds = useWorlds();
  const worldNames = useMemo(() => buildWorldNameMap(worlds.data), [worlds.data]);
  const ids = useMemo(() => (catalogue.data ? boardItemIds(catalogue.data) : []), [catalogue.data]);
  const { query: listings, progress } = useMarketListings(target, ids);

  const planner = useMemo(
    () =>
      catalogue.data && listings.data
        ? createPlanner(catalogue.data.recipes, listings.data.board)
        : null,
    [catalogue.data, listings.data],
  );

  const rows = useMemo(() => {
    if (!planner || !catalogue.data || !listings.data) return [];
    const current: GcSealsMode =
      mode === 'quantity' ? { kind: 'quantity', quantity } : { kind: 'seals', sealTarget };
    return computeRows(catalogue.data, listings.data.board, current, planner);
  }, [planner, catalogue.data, listings.data, mode, quantity, sealTarget]);

  const visibleRows = useMemo(
    () => (showHidden ? rows : rows.filter((r) => !ignoredItemIds.includes(r.id))),
    [rows, showHidden, ignoredItemIds],
  );
  const max = mode === 'quantity' ? MAX_GC_QUANTITY : MAX_SEAL_TARGET;
  // Plain consts so the narrowing below carries into the breakdown closure.
  const result = listings.data;
  const names = catalogue.data?.names;
  const progressNotice = (
    <div className={NOTICE_CLASS}>
      Fetching market data from Universalis… {progress.done}/{progress.total || '?'}
    </div>
  );

  const commitAmount = () => {
    const current = mode === 'quantity' ? quantity : sealTarget;
    const next = parseBoundedInt(amountDraft, max) ?? current;
    setAmountDraft(String(next));
    if (next === current) return;
    patchPrefs((prev) => ({
      gcSeals:
        mode === 'quantity'
          ? { ...prev.gcSeals, quantity: next }
          : { ...prev.gcSeals, sealTarget: next },
    }));
  };

  const switchMode = (next: ModeKind) => {
    if (next === mode) return;
    patchPrefs((prev) => ({ gcSeals: { ...prev.gcSeals, mode: next } }));
    setAmountDraft(String(next === 'quantity' ? quantity : sealTarget));
  };

  // Enter in the amount field only applies the number; it must not refetch prices.
  const onAmountKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    commitAmount();
  };

  const onLoad = (e: FormEvent) => {
    e.preventDefault();
    commitAmount();
    if (!location) {
      setLocationError('Pick a location');
      return;
    }
    setLocationError(null);
    const next: ListingsTarget = {
      location: location.name,
      worldId: location.kind === 'world' ? location.worldId : undefined,
    };
    if (target?.location === next.location) {
      void listings.refetch();
    } else {
      setTarget(next);
    }
  };

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">seal solver</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">GC seals</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          The cheapest Grand Company seals: every Expert Delivery item, priced by buying the
          finished item or crafting it, whichever is cheaper at each step of the recipe. Click a row
          for the plan and shopping list. Prices are per unit from the cheapest listings.
        </p>
      </header>

      <form
        onSubmit={onLoad}
        className="flex flex-wrap items-end gap-4 rounded-xl border border-border/60 bg-card/40 p-4"
      >
        <div className="flex flex-col gap-1.5">
          <TieredLocationSelect value={location} onChange={setLocation} />
          {locationError && <span className="text-xs text-destructive">{locationError}</span>}
        </div>

        <div className="flex flex-col gap-1.5">
          <span className={LABEL_CLASS}>Price for</span>
          <div role="radiogroup" className="flex rounded-md border border-border/60 p-0.5 text-sm">
            {(['quantity', 'seals'] as const).map((m) => (
              <button
                key={m}
                type="button"
                role="radio"
                aria-checked={mode === m}
                onClick={() => switchMode(m)}
                className={[
                  'rounded px-3 py-1.5 transition-colors',
                  mode === m
                    ? 'bg-accent text-accent-foreground'
                    : 'text-muted-foreground hover:text-foreground',
                ].join(' ')}
              >
                {m === 'quantity' ? 'Quantity' : 'Seal target'}
              </button>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="gc-seals-amount" className={LABEL_CLASS}>
            {mode === 'quantity' ? 'Items each' : 'Seals wanted'}
          </label>
          <input
            id="gc-seals-amount"
            type="number"
            inputMode="numeric"
            min={1}
            max={max}
            value={amountDraft}
            onChange={(e) => setAmountDraft(e.target.value)}
            onBlur={commitAmount}
            onKeyDown={onAmountKeyDown}
            className={INPUT_CLASS}
          />
        </div>

        <button
          type="submit"
          disabled={listings.isFetching || !catalogue.data}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-foreground transition-colors hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {listings.isFetching ? 'Loading…' : target ? 'Reload prices' : 'Load prices'}
        </button>
      </form>

      <section className="space-y-3">
        {catalogue.isLoading ? (
          <div className={QUERY_SKELETON_CLASS} />
        ) : catalogue.isError || !catalogue.data ? (
          <div className={ERROR_CLASS}>
            Could not load the expert delivery catalogue. Try again later.
          </div>
        ) : ids.length === 0 ? (
          <EmptyState>
            The expert delivery catalogue is empty right now. Try again later.
          </EmptyState>
        ) : target === null ? (
          <EmptyState>
            Pick a location and load prices. They come straight from Universalis; a whole region
            takes a few seconds.
          </EmptyState>
        ) : listings.isError ? (
          <div className={ERROR_CLASS}>{(listings.error as Error).message}</div>
        ) : !result || !planner || !names ? (
          progressNotice
        ) : (
          <>
            {listings.isFetching && progressNotice}
            <header className="flex flex-wrap items-baseline justify-between gap-3 text-xs text-muted-foreground">
              <span>
                <span className="font-mono text-foreground">{visibleRows.length}</span> items on{' '}
                <span className="font-mono text-foreground">{target.location}</span>
              </span>
              <CheckboxToggle
                size="xs"
                label="Show hidden items"
                checked={showHidden}
                onChange={(v) => patchPrefs({ showHidden: v })}
              />
            </header>
            {result.failedChunks > 0 && (
              <p className={ERROR_CLASS}>
                {result.failedChunks} of {result.totalChunks} Universalis requests failed - some
                items may show as short. Reload to try again.
              </p>
            )}
            <GcSealsTable
              rows={visibleRows}
              emptyMessage={
                rows.length > 0
                  ? 'Every item is hidden. Tick Show hidden items to see them.'
                  : undefined
              }
              renderBreakdown={(row) => (
                <GcSealsBreakdownView
                  breakdown={buildBreakdown(row, planner, result.board)}
                  names={names}
                  worldNames={worldNames}
                />
              )}
              ignoredItemIds={showHidden ? ignoredItemIds : undefined}
              onIgnore={ignore}
              onUnignore={showHidden ? unignore : undefined}
            />
          </>
        )}
      </section>
    </div>
  );
}

import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { Td, Th } from '../../components/data/TableCells';
import EmptyState from '../../components/layout/EmptyState';
import { QUERY_SKELETON_CLASS } from '../../components/layout/QueryBoundary';
import { useItemNames } from '../../hooks/useItemNames';
import {
  useMannequinSales,
  type MannequinFilters,
  type MannequinQuality,
} from '../../hooks/useMannequinSales';
import { patchPrefs, useUserPrefs } from '../../hooks/useUserPrefs';
import { useWorlds } from '../../hooks/useWorlds';
import { formatGilExact } from '../../lib/format';
import {
  accumulateHead,
  formatMinPrice,
  mannequinSaleKey,
  mergeSales,
  newSaleKeys,
  parseMinPrice,
} from '../../lib/mannequin';
import { relativeTime } from '../../lib/time';
import { buildWorldNameMap } from '../../lib/worlds';
import type { MannequinSale, WorldStructure } from '../../api/types';

const HIGHLIGHT_MS = 5_000;
const AUTO_CONTINUE_MAX_PAGES = 5;

const LABEL_CLASS = 'text-xs uppercase tracking-widest text-muted-foreground';
const SELECT_CLASS =
  'rounded-md border border-border/60 bg-card px-3 py-2 text-sm text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50 disabled:cursor-not-allowed disabled:opacity-50';

export default function MannequinSalesPage() {
  const [prefs] = useUserPrefs();
  const filters = prefs.mannequinFilters;
  const setFilters = (patch: Partial<MannequinFilters>) =>
    patchPrefs((prev) => ({ mannequinFilters: { ...prev.mannequinFilters, ...patch } }));

  const scope = `${filters.datacenter}|${filters.world}|${filters.minUnitPrice}|${filters.quality}`;
  const { head, history } = useMannequinSales(filters);
  const headRows = useAccumulatedHead(head.data?.data, scope);
  const rows = useMemo(
    () => mergeSales(headRows, history.data?.pages.flatMap((p) => p.data) ?? []),
    [headRows, history.data],
  );
  const highlighted = useNewRowHighlight(rows, scope);
  const isError = head.isError || history.isError;

  // A quiet location can return an empty page that still has older history behind it.
  // Capped so a location with no mannequin sales doesn't walk the whole day index, and
  // stops on a failed page so it doesn't retry forever.
  const lastPage = history.data?.pages.at(-1);
  const canAutoContinue = (history.data?.pages.length ?? 0) <= AUTO_CONTINUE_MAX_PAGES;
  const { hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage } = history;
  useEffect(() => {
    if (
      lastPage &&
      lastPage.data.length === 0 &&
      canAutoContinue &&
      hasNextPage &&
      !isFetchingNextPage &&
      !isFetchNextPageError
    ) {
      void fetchNextPage();
    }
  }, [
    lastPage,
    canAutoContinue,
    hasNextPage,
    isFetchingNextPage,
    isFetchNextPageError,
    fetchNextPage,
  ]);
  const searchingOlder = rows.length === 0 && !isError && hasNextPage && canAutoContinue;

  const worlds = useWorlds();
  const worldNameMap = useMemo(() => buildWorldNameMap(worlds.data), [worlds.data]);
  const itemIds = useMemo(() => rows.map((r) => r.item_id), [rows]);
  const itemNameMap = useItemNames(itemIds);

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">mannequin sales</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Mannequin sales</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Recent purchases made from retainer mannequins.
        </p>
      </header>

      <div className="flex flex-wrap items-end gap-4 rounded-xl border border-border/60 bg-card/40 p-4">
        <DatacenterSelect
          worlds={worlds.data}
          value={filters.datacenter}
          onChange={(datacenter) => setFilters({ datacenter, world: '' })}
        />
        {filters.datacenter && (
          <WorldSelect
            worlds={worldsInDatacenter(worlds.data, filters.datacenter)}
            value={filters.world}
            onChange={(world) => setFilters({ world })}
          />
        )}
        <QualitySelect value={filters.quality} onChange={(quality) => setFilters({ quality })} />
        <MinPriceInput
          value={filters.minUnitPrice}
          onCommit={(minUnitPrice) => setFilters({ minUnitPrice })}
        />
      </div>

      <section className="space-y-3">
        {history.isLoading ? (
          <div className={QUERY_SKELETON_CLASS} />
        ) : (
          <>
            {rows.length > 0 ? (
              <ResultsTable
                rows={rows}
                highlighted={highlighted}
                worldNameMap={worldNameMap}
                itemNameMap={itemNameMap}
              />
            ) : (
              !isError && (
                <EmptyState>
                  {searchingOlder
                    ? 'Searching older history…'
                    : hasNextPage
                      ? 'No mannequin sales in recent history.'
                      : 'No mannequin sales found.'}
                </EmptyState>
              )
            )}
            {isError && <LoadError />}
            {hasNextPage && !searchingOlder && (
              <button
                type="button"
                onClick={() => void fetchNextPage()}
                disabled={isFetchingNextPage}
                className="rounded-md border border-border/60 bg-card px-4 py-2 text-sm text-muted-foreground transition-colors hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isFetchingNextPage ? 'Loading…' : 'Load older'}
              </button>
            )}
          </>
        )}
      </section>
    </div>
  );
}

function LoadError() {
  return (
    <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
      Failed to load mannequin sales.
    </div>
  );
}

function DatacenterSelect({
  worlds,
  value,
  onChange,
}: {
  worlds: WorldStructure | undefined;
  value: string;
  onChange: (next: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor="mannequin-datacenter" className={LABEL_CLASS}>
        Datacenter
      </label>
      <select
        id="mannequin-datacenter"
        value={value}
        disabled={!worlds}
        onChange={(e) => onChange(e.target.value)}
        className={SELECT_CLASS}
      >
        <option value="">All</option>
        {Object.entries(worlds ?? {}).map(([region, dcs]) => (
          <optgroup key={region} label={region}>
            {Object.keys(dcs).map((dc) => (
              <option key={dc} value={dc}>
                {dc}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
    </div>
  );
}

function WorldSelect({
  worlds,
  value,
  onChange,
}: {
  worlds: string[];
  value: string;
  onChange: (next: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor="mannequin-world" className={LABEL_CLASS}>
        World
      </label>
      <select
        id="mannequin-world"
        value={value}
        disabled={worlds.length === 0}
        onChange={(e) => onChange(e.target.value)}
        className={SELECT_CLASS}
      >
        <option value="">All</option>
        {worlds.map((w) => (
          <option key={w} value={w}>
            {w}
          </option>
        ))}
      </select>
    </div>
  );
}

function worldsInDatacenter(tree: WorldStructure | undefined, datacenter: string): string[] {
  for (const dcs of Object.values(tree ?? {})) {
    const worlds = dcs[datacenter];
    if (worlds) return Object.values(worlds).sort((a, b) => a.localeCompare(b));
  }
  return [];
}

function QualitySelect({
  value,
  onChange,
}: {
  value: MannequinQuality;
  onChange: (next: MannequinQuality) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor="mannequin-quality" className={LABEL_CLASS}>
        Quality
      </label>
      <select
        id="mannequin-quality"
        value={value}
        onChange={(e) => onChange(e.target.value as MannequinQuality)}
        className={SELECT_CLASS}
      >
        <option value="all">All</option>
        <option value="hq">HQ Only</option>
        <option value="nq">NQ Only</option>
      </select>
    </div>
  );
}

// Committed on blur or Enter so typing a price doesn't fire a request per keystroke.
function MinPriceInput({ value, onCommit }: { value: number; onCommit: (next: number) => void }) {
  const [draft, setDraft] = useState(formatMinPrice(value));

  const commit = () => {
    const next = parseMinPrice(draft);
    if (next !== value) onCommit(next);
  };

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    commit();
  };

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-1.5">
      <label htmlFor="mannequin-min-price" className={LABEL_CLASS}>
        Min unit price
      </label>
      <input
        id="mannequin-min-price"
        type="text"
        inputMode="numeric"
        placeholder="0"
        value={draft}
        onChange={(e) => setDraft(formatMinPrice(parseMinPrice(e.target.value)))}
        onBlur={commit}
        className="w-36 rounded-md border border-border/60 bg-card px-3 py-2 text-sm text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50"
      />
    </form>
  );
}

// The history's first page is never refetched, so rows pushed off the head between polls
// would otherwise vanish. Derived during render; a new scope starts from nothing.
function useAccumulatedHead(latest: MannequinSale[] | undefined, scope: string): MannequinSale[] {
  const [acc, setAcc] = useState<{
    scope: string;
    latest: MannequinSale[] | undefined;
    rows: MannequinSale[];
  }>({ scope, latest: undefined, rows: [] });

  if (acc.scope === scope && acc.latest === latest) return acc.rows;
  const rows = accumulateHead(acc.scope === scope ? acc.rows : [], latest ?? []);
  setAcc({ scope, latest, rows });
  return rows;
}

function useNewRowHighlight(rows: MannequinSale[], scope: string): Set<string> {
  const prev = useRef<{ scope: string; rows: MannequinSale[] } | null>(null);
  const [highlighted, setHighlighted] = useState<Set<string>>(() => new Set());

  useEffect(() => {
    const before = prev.current?.scope === scope ? prev.current.rows : undefined;
    prev.current = { scope, rows };
    const fresh = newSaleKeys(before, rows);
    if (fresh.size > 0) setHighlighted(fresh);
  }, [rows, scope]);

  // Its own effect so a rows change with nothing new (load older, a filter switch)
  // can't cancel the pending clear and leave rows lit.
  useEffect(() => {
    if (highlighted.size === 0) return;
    const id = setTimeout(() => setHighlighted(new Set()), HIGHLIGHT_MS);
    return () => clearTimeout(id);
  }, [highlighted]);

  return highlighted;
}

function ResultsTable({
  rows,
  highlighted,
  worldNameMap,
  itemNameMap,
}: {
  rows: MannequinSale[];
  highlighted: Set<string>;
  worldNameMap: Map<number, string>;
  itemNameMap: Map<number, string>;
}) {
  return (
    <div className="overflow-hidden rounded-xl border border-border/60">
      <table className="w-full text-sm">
        <thead className="bg-card/60 text-xs uppercase tracking-widest text-muted-foreground">
          <tr>
            <Th>Item</Th>
            <Th>World</Th>
            <Th>Buyer</Th>
            <Th align="right">Total</Th>
            <Th>When</Th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const key = mannequinSaleKey(row);
            return (
              <tr
                key={key}
                className={`border-t border-border/40 transition-colors ${
                  highlighted.has(key) ? 'bg-accent/5' : 'even:bg-card/20'
                }`}
              >
                <Td>
                  <span className="flex items-center gap-1.5">
                    <Link to={`/item/${row.item_id}`} className="text-accent hover:underline">
                      {itemNameMap.get(row.item_id) ?? `#${row.item_id}`}
                    </Link>
                    {row.hq && (
                      <span className="shrink-0 rounded border border-accent/30 bg-accent/10 px-1 py-px font-mono text-[10px] font-semibold text-accent">
                        HQ
                      </span>
                    )}
                  </span>
                </Td>
                <Td>{worldNameMap.get(row.world_id) ?? String(row.world_id)}</Td>
                <Td muted>{row.buyer_name}</Td>
                <Td align="right" mono>
                  {formatGilExact(row.total_price)}
                </Td>
                <Td>
                  <span title={row.sale_time}>{relativeTime(row.sale_time)}</span>
                </Td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

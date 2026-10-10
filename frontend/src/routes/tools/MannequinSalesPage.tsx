import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { Td, Th } from '../../components/data/TableCells';
import CheckboxToggle from '../../components/form/CheckboxToggle';
import TieredLocationSelect from '../../components/form/TieredLocationSelect';
import EmptyState from '../../components/layout/EmptyState';
import { QUERY_SKELETON_CLASS } from '../../components/layout/QueryBoundary';
import { useItemNames } from '../../hooks/useItemNames';
import { useMannequinSales, type MannequinFilters } from '../../hooks/useMannequinSales';
import { patchPrefs, useUserPrefs } from '../../hooks/useUserPrefs';
import { useWorlds } from '../../hooks/useWorlds';
import { formatGilExact, formatNumber } from '../../lib/format';
import { mannequinSaleKey, newSaleKeys } from '../../lib/mannequin';
import { relativeTime } from '../../lib/time';
import { buildWorldNameMap } from '../../lib/worlds';
import type { Location, MannequinSale } from '../../api/types';

const HIGHLIGHT_MS = 5_000;

export default function MannequinSalesPage() {
  const [prefs] = useUserPrefs();
  const location = prefs.lastLocation;
  const setLocation = useCallback((next: Location) => patchPrefs({ lastLocation: next }), []);

  const filters = prefs.mannequinFilters;
  const setFilters = (patch: Partial<MannequinFilters>) =>
    patchPrefs((prev) => ({ mannequinFilters: { ...prev.mannequinFilters, ...patch } }));

  const query = useMannequinSales(location?.name, filters);
  const rows = useMemo(() => query.data?.pages.flatMap((p) => p.data) ?? [], [query.data]);
  const highlighted = useNewRowHighlight(
    rows,
    `${location?.name}|${filters.hqOnly}|${filters.minUnitPrice}`,
  );

  // A quiet location can return an empty page that still has older history behind it.
  // Stops on a failed page so it doesn't retry forever; the next refetch resumes it.
  const lastPage = query.data?.pages.at(-1);
  const { hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage } = query;
  useEffect(() => {
    if (
      lastPage &&
      lastPage.data.length === 0 &&
      hasNextPage &&
      !isFetchingNextPage &&
      !isFetchNextPageError
    ) {
      void fetchNextPage();
    }
  }, [lastPage, hasNextPage, isFetchingNextPage, isFetchNextPageError, fetchNextPage]);

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
        <TieredLocationSelect value={location} onChange={setLocation} />
        <MinPriceInput
          value={filters.minUnitPrice}
          onCommit={(minUnitPrice) => setFilters({ minUnitPrice })}
        />
        <CheckboxToggle
          label="HQ only"
          checked={filters.hqOnly}
          onChange={(hqOnly) => setFilters({ hqOnly })}
        />
      </div>

      <section className="space-y-3">
        {!location ? (
          <EmptyState>Pick a world, datacenter or region.</EmptyState>
        ) : query.isLoading ? (
          <div className={QUERY_SKELETON_CLASS} />
        ) : query.isError ? (
          <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
            Failed to load mannequin sales.
          </div>
        ) : rows.length === 0 ? (
          <EmptyState>
            {hasNextPage ? 'Searching older history…' : 'No mannequin sales found.'}
          </EmptyState>
        ) : (
          <>
            <ResultsTable
              rows={rows}
              highlighted={highlighted}
              worldNameMap={worldNameMap}
              itemNameMap={itemNameMap}
            />
            {hasNextPage && (
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

// Committed on blur or Enter so typing a price doesn't fire a request per keystroke.
function MinPriceInput({ value, onCommit }: { value: number; onCommit: (next: number) => void }) {
  const [draft, setDraft] = useState(value > 0 ? String(value) : '');

  const commit = () => {
    const next = Math.max(0, Math.floor(Number(draft) || 0));
    if (next !== value) onCommit(next);
  };

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    commit();
  };

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-1.5">
      <label
        htmlFor="mannequin-min-price"
        className="text-xs uppercase tracking-widest text-muted-foreground"
      >
        Min unit price
      </label>
      <input
        id="mannequin-min-price"
        type="number"
        min={0}
        step={1}
        inputMode="numeric"
        placeholder="0"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        className="w-36 rounded-md border border-border/60 bg-card px-3 py-2 text-sm text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50"
      />
    </form>
  );
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
            <Th align="right">Qty</Th>
            <Th align="right">Unit price</Th>
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
                  {formatNumber(row.quantity)}
                </Td>
                <Td align="right" mono>
                  {formatGilExact(row.unit_price)}
                </Td>
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

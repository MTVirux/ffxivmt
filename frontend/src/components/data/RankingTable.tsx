import { Link } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  getExpandedRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type FilterFn,
  type SortingState,
} from '@tanstack/react-table';
import { useVirtualizer } from '@tanstack/react-virtual';
import { useMemo, useRef, useState } from 'react';
import type { RankingRow } from '../../lib/rankingAggregate';
import { TIMEFRAMES } from '../../lib/rankingAggregate';
import { formatGilCompact } from '../../lib/format';
import { relativeTime } from '../../lib/time';
import TableSearch from '../form/TableSearch';
import { matchesItemName } from '../../lib/itemFilter';

const EST_ROW_HEIGHT = 37;

type Props = {
  rows: RankingRow[];
  /** True when the response covers more than one world. Toggles the expand column. */
  showWorldExpand: boolean;
  timeframes?: readonly { key: string; label: string }[];
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

const nameFilter: FilterFn<RankingRow> = (row, _columnId, value) =>
  matchesItemName(row.original.item_name, value as string);

export default function RankingTable({
  rows,
  showWorldExpand,
  timeframes: timeframesProp,
  ignoredItemIds,
  onIgnore,
  onUnignore,
}: Props) {
  const [sorting, setSorting] = useState<SortingState>([{ id: '1h', desc: true }]);
  const [globalFilter, setGlobalFilter] = useState('');
  const timeframes = timeframesProp ?? TIMEFRAMES;

  const columns = useMemo<ColumnDef<RankingRow>[]>(() => {
    const cols: ColumnDef<RankingRow>[] = [
      {
        id: 'item',
        header: 'Item',
        accessorKey: 'item_name',
        cell: ({ row }) => {
          const r = row.original;
          const indent = row.depth * 20;
          if (r.kind === 'world') {
            return (
              <div style={{ paddingLeft: indent }} className="text-sm text-muted-foreground">
                <span className="font-mono text-xs">{r.world_name ?? '—'}</span>
              </div>
            );
          }
          const isIgnored = ignoredItemIds?.includes(r.item_id) ?? false;
          return (
            <div className="flex min-w-0 items-center gap-2" style={{ paddingLeft: indent }}>
              {showWorldExpand && row.getCanExpand() ? (
                <button
                  type="button"
                  onClick={row.getToggleExpandedHandler()}
                  aria-label={row.getIsExpanded() ? 'Collapse' : 'Expand'}
                  className="rounded text-muted-foreground hover:text-foreground"
                >
                  <span className="inline-block w-4 text-center font-mono">
                    {row.getIsExpanded() ? '▾' : '▸'}
                  </span>
                </button>
              ) : (
                <span className="inline-block w-4" />
              )}
              <Link
                to={`/item/${r.item_id}`}
                className="min-w-0 truncate font-medium text-foreground hover:text-accent"
              >
                {r.item_name}
              </Link>
              {isIgnored && onUnignore ? (
                <button
                  type="button"
                  onClick={() => onUnignore(r.item_id)}
                  className="ml-1 text-xs text-muted-foreground hover:text-foreground"
                  aria-label="Unhide item"
                >
                  Unhide
                </button>
              ) : !isIgnored && onIgnore ? (
                <button
                  type="button"
                  onClick={() => onIgnore(r.item_id)}
                  className="ml-1 text-xs text-muted-foreground hover:text-destructive"
                  aria-label="Ignore item"
                >
                  ×
                </button>
              ) : null}
            </div>
          );
        },
      },
      ...timeframes.map<ColumnDef<RankingRow>>((tf) => ({
        id: tf.key,
        header: tf.label,
        accessorFn: (row) => row.rankings[tf.key] ?? 0,
        sortingFn: 'basic',
        cell: ({ getValue, row }) => {
          const v = getValue<number>();
          return (
            <span
              className={`font-mono tabular-nums text-sm ${
                row.original.kind === 'world' ? 'text-muted-foreground' : 'text-foreground'
              }`}
            >
              {v > 0 ? formatGilCompact(v) : <span className="text-muted-foreground">—</span>}
            </span>
          );
        },
      })),
      {
        id: 'last_sale',
        header: 'Last sale',
        accessorKey: 'last_sale_time',
        sortingFn: 'basic',
        cell: ({ getValue }) => {
          const v = getValue<number | null>();
          return (
            <span className="text-xs text-muted-foreground">
              {v !== null ? relativeTime(v) : '—'}
            </span>
          );
        },
      },
    ];
    return cols;
  }, [showWorldExpand, timeframes, ignoredItemIds, onIgnore, onUnignore]);

  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    globalFilterFn: nameFilter,
    getSubRows: (row) => (showWorldExpand ? row.subRows : undefined),
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getExpandedRowModel: getExpandedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
  });

  const modelRows = table.getRowModel().rows;
  const scrollerRef = useRef<HTMLDivElement | null>(null);
  const virtualizer = useVirtualizer({
    count: modelRows.length,
    getScrollElement: () => scrollerRef.current,
    estimateSize: () => EST_ROW_HEIGHT,
    overscan: 12,
  });

  const gridTemplate = useMemo(() => {
    const tfCols = timeframes.map(() => '7.5rem').join(' ');
    return `minmax(0, 1fr) ${tfCols} 8rem`;
  }, [timeframes]);

  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
        No rankings yet for this location.
      </div>
    );
  }

  const topLevelMatches = table.getFilteredRowModel().rows.length;

  return (
    <div>
      <TableSearch
        value={globalFilter}
        onChange={setGlobalFilter}
        resultCount={topLevelMatches}
        totalCount={rows.length}
      />
      {topLevelMatches === 0 ? (
        <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
          No items match “{globalFilter}”.
        </div>
      ) : (
        <div
          ref={scrollerRef}
          role="table"
          aria-rowcount={modelRows.length}
          className="overflow-auto rounded-xl border border-border/60 text-sm"
          style={{ maxHeight: 'calc(100vh - 22rem)' }}
        >
          <div
            role="rowgroup"
            className="sticky top-0 z-10 bg-card/80 backdrop-blur text-xs uppercase tracking-widest text-muted-foreground"
          >
            {table.getHeaderGroups().map((hg) => (
              <div
                key={hg.id}
                role="row"
                className="grid border-b border-border/60"
                style={{ gridTemplateColumns: gridTemplate }}
              >
                {hg.headers.map((h) => {
                  const numeric = h.column.id !== 'item';
                  const sort = h.column.getIsSorted();
                  const canSort = h.column.getCanSort();
                  return (
                    <div
                      key={h.id}
                      role="columnheader"
                      aria-sort={
                        sort === 'asc' ? 'ascending' : sort === 'desc' ? 'descending' : 'none'
                      }
                      onClick={canSort ? h.column.getToggleSortingHandler() : undefined}
                      className={[
                        'px-3 py-2 font-medium',
                        numeric ? 'text-right' : 'text-left',
                        canSort ? 'cursor-pointer select-none hover:text-foreground' : '',
                      ]
                        .filter(Boolean)
                        .join(' ')}
                    >
                      <span
                        className={`inline-flex items-center gap-1 ${numeric ? 'w-full justify-end' : ''}`}
                      >
                        {flexRender(h.column.columnDef.header, h.getContext())}
                        {sort === 'asc' && <span aria-hidden="true">▲</span>}
                        {sort === 'desc' && <span aria-hidden="true">▼</span>}
                      </span>
                    </div>
                  );
                })}
              </div>
            ))}
          </div>

          <div role="rowgroup" style={{ height: virtualizer.getTotalSize(), position: 'relative' }}>
            {virtualizer.getVirtualItems().map((vi) => {
              const row = modelRows[vi.index];
              const r = row.original;
              const isIgnored = r.kind === 'aggregate' && ignoredItemIds?.includes(r.item_id);
              return (
                <div
                  key={row.id}
                  role="row"
                  aria-rowindex={vi.index + 2}
                  data-index={vi.index}
                  ref={virtualizer.measureElement}
                  className={[
                    'grid border-t border-border/40',
                    r.kind === 'world' ? 'bg-card/20' : 'hover:bg-card/40',
                    isIgnored ? 'opacity-50' : '',
                  ]
                    .filter(Boolean)
                    .join(' ')}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    transform: `translateY(${vi.start}px)`,
                    gridTemplateColumns: gridTemplate,
                  }}
                >
                  {row.getVisibleCells().map((cell) => {
                    const numeric = cell.column.id !== 'item';
                    return (
                      <div
                        key={cell.id}
                        role="cell"
                        className={['min-w-0 px-3 py-2', numeric ? 'text-right' : 'text-left'].join(
                          ' ',
                        )}
                      >
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </div>
                    );
                  })}
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}

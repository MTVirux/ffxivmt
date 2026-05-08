import { Link } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  getExpandedRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import type { RankingRow } from '../../lib/rankingAggregate';
import { TIMEFRAMES, type TimeframeKey } from '../../lib/rankingAggregate';
import { formatGil } from '../../lib/format';
import { relativeTime } from '../../lib/time';

type Props = {
  rows: RankingRow[];
  /** True when the response covers more than one world. Toggles the expand column. */
  showWorldExpand: boolean;
};

export default function RankingTable({ rows, showWorldExpand }: Props) {
  const [sorting, setSorting] = useState<SortingState>([{ id: 'ranking_1h', desc: true }]);

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
          return (
            <div className="flex items-center gap-2" style={{ paddingLeft: indent }}>
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
                className="font-medium text-foreground hover:text-accent"
              >
                {r.item_name}
              </Link>
            </div>
          );
        },
      },
      ...TIMEFRAMES.map<ColumnDef<RankingRow>>((tf) => ({
        id: tf.key,
        header: tf.label,
        accessorFn: (row) => row[tf.key as TimeframeKey],
        sortingFn: 'basic',
        cell: ({ getValue, row }) => {
          const v = getValue<number>();
          return (
            <span
              className={`font-mono tabular-nums text-sm ${
                row.original.kind === 'world' ? 'text-muted-foreground' : 'text-foreground'
              }`}
            >
              {v > 0 ? formatGil(v) : <span className="text-muted-foreground">—</span>}
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
  }, [showWorldExpand]);

  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getSubRows: (row) => (showWorldExpand ? row.subRows : undefined),
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getExpandedRowModel: getExpandedRowModel(),
  });

  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
        No rankings yet for this location.
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-border/60">
      <table className="w-full text-sm">
        <thead className="bg-card/60 text-xs uppercase tracking-widest text-muted-foreground">
          {table.getHeaderGroups().map((hg) => (
            <tr key={hg.id}>
              {hg.headers.map((h) => {
                const numeric = h.column.id !== 'item';
                const sort = h.column.getIsSorted();
                const canSort = h.column.getCanSort();
                return (
                  <th
                    key={h.id}
                    scope="col"
                    className={[
                      'px-3 py-2 font-medium',
                      numeric ? 'text-right' : 'text-left',
                      canSort ? 'cursor-pointer select-none hover:text-foreground' : '',
                    ]
                      .filter(Boolean)
                      .join(' ')}
                    onClick={canSort ? h.column.getToggleSortingHandler() : undefined}
                  >
                    <span className="inline-flex items-center gap-1">
                      {flexRender(h.column.columnDef.header, h.getContext())}
                      {sort === 'asc' && <span aria-hidden="true">▲</span>}
                      {sort === 'desc' && <span aria-hidden="true">▼</span>}
                    </span>
                  </th>
                );
              })}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((row) => (
            <tr
              key={row.id}
              className={[
                'border-t border-border/40',
                row.original.kind === 'world' ? 'bg-card/20' : 'hover:bg-card/40',
              ].join(' ')}
            >
              {row.getVisibleCells().map((cell) => {
                const numeric = cell.column.id !== 'item';
                return (
                  <td
                    key={cell.id}
                    className={[
                      'px-3 py-2',
                      numeric ? 'text-right' : 'text-left',
                    ].join(' ')}
                  >
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

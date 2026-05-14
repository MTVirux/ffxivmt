import { Link } from 'react-router-dom';
import {
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table';
import { useMemo, useState } from 'react';
import type { ProfitRow } from '../../api/types';
import { formatGil, formatNumber } from '../../lib/format';

type Props = {
  rows: ProfitRow[];
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

export default function ProfitTable({ rows, ignoredItemIds, onIgnore, onUnignore }: Props) {
  const columns = useMemo<ColumnDef<ProfitRow>[]>(() => {
    const base: ColumnDef<ProfitRow>[] = [
      {
        id: 'name',
        header: 'Item',
        accessorKey: 'name',
        cell: ({ row, getValue }) => (
          <Link
            to={`/item/${row.original.id}`}
            className="font-medium text-foreground hover:text-accent"
          >
            {getValue<string>()}
          </Link>
        ),
      },
      {
        id: 'min_price',
        header: 'Min price',
        accessorKey: 'min_price',
        sortingFn: 'basic',
        cell: ({ getValue }) => (
          <span className="font-mono tabular-nums text-sm">{formatGil(getValue<number>())}</span>
        ),
      },
      {
        id: 'regular_sale_velocity',
        header: 'Velocity',
        accessorKey: 'regular_sale_velocity',
        sortingFn: 'basic',
        cell: ({ getValue }) => (
          <span className="font-mono tabular-nums text-sm text-muted-foreground">
            {formatNumber(Math.round(getValue<number>() * 100) / 100)} /day
          </span>
        ),
      },
      {
        id: 'ffmt_score',
        header: 'Score',
        accessorKey: 'ffmt_score',
        sortingFn: 'basic',
        cell: ({ getValue }) => (
          <span className="font-mono tabular-nums text-sm font-medium text-accent">
            {formatNumber(Math.round(getValue<number>()))}
          </span>
        ),
      },
    ];
    if (onIgnore || onUnignore) {
      base.push({
        id: 'actions',
        header: '',
        cell: ({ row }) => {
          const id = row.original.id;
          const isIgnored = ignoredItemIds?.includes(id) ?? false;
          if (isIgnored && onUnignore) {
            return (
              <button
                type="button"
                onClick={() => onUnignore(id)}
                className="text-xs text-muted-foreground hover:text-foreground"
                aria-label="Unhide item"
              >
                Unhide
              </button>
            );
          }
          if (!isIgnored && onIgnore) {
            return (
              <button
                type="button"
                onClick={() => onIgnore(id)}
                className="text-xs text-muted-foreground hover:text-destructive"
                aria-label="Ignore item"
              >
                ×
              </button>
            );
          }
          return null;
        },
      });
    }
    return base;
  }, [ignoredItemIds, onIgnore, onUnignore]);

  const [sorting, setSorting] = useState<SortingState>([{ id: 'ffmt_score', desc: true }]);
  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });

  if (rows.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
        No matching market-board data.
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
                const numeric = h.column.id !== 'name';
                const sort = h.column.getIsSorted();
                return (
                  <th
                    key={h.id}
                    scope="col"
                    className={[
                      'cursor-pointer select-none px-3 py-2 font-medium hover:text-foreground',
                      numeric ? 'text-right' : 'text-left',
                    ].join(' ')}
                    onClick={h.column.getToggleSortingHandler()}
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
                'border-t border-border/40 hover:bg-card/30',
                ignoredItemIds?.includes(row.original.id) ? 'opacity-50' : '',
              ]
                .filter(Boolean)
                .join(' ')}
            >
              {row.getVisibleCells().map((cell) => {
                const numeric = cell.column.id !== 'name';
                return (
                  <td
                    key={cell.id}
                    className={['px-3 py-2', numeric ? 'text-right' : 'text-left'].join(' ')}
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

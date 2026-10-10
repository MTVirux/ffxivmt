import { Link } from 'react-router';
import { type ColumnDef, type SortingState } from '@tanstack/react-table';
import { useMemo, type ReactNode } from 'react';
import type { GcSealsRow } from '../../lib/gcSeals';
import { formatGilCompact, formatNumber } from '../../lib/format';
import DataTable, { makeActionsColumn } from './DataTable';

type Props = {
  rows: GcSealsRow[];
  renderBreakdown: (row: GcSealsRow) => ReactNode;
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

const DEFAULT_SORT: SortingState = [{ id: 'gil_per_seal', desc: false }];

// Unpriceable rows carry null; sorting them as +Infinity keeps them at the bottom.
const orLast = (value: number | null) => value ?? Number.POSITIVE_INFINITY;

const rowId = (row: GcSealsRow) => String(row.id);

function Gil({ value }: { value: number | null }) {
  return (
    <span className="font-mono text-sm tabular-nums">
      {value === null ? '-' : formatGilCompact(value)}
    </span>
  );
}

function Num({ value }: { value: number }) {
  return (
    <span className="font-mono text-sm tabular-nums text-muted-foreground">
      {formatNumber(value)}
    </span>
  );
}

export default function GcSealsTable({
  rows,
  renderBreakdown,
  ignoredItemIds,
  onIgnore,
  onUnignore,
}: Props) {
  const columns = useMemo<ColumnDef<GcSealsRow>[]>(() => {
    const base: ColumnDef<GcSealsRow>[] = [
      {
        id: 'name',
        header: 'Item',
        accessorKey: 'name',
        cell: ({ row, getValue }) => (
          <Link
            to={`/item/${row.original.id}`}
            onClick={(e) => e.stopPropagation()}
            className="font-medium text-foreground hover:text-accent"
          >
            {getValue<string>()}
          </Link>
        ),
      },
      {
        id: 'item_level',
        header: 'iLvl',
        accessorKey: 'item_level',
        sortingFn: 'basic',
        cell: ({ getValue }) => <Num value={getValue<number>()} />,
      },
      {
        id: 'seals',
        header: 'Seals',
        accessorKey: 'seals',
        sortingFn: 'basic',
        cell: ({ getValue }) => <Num value={getValue<number>()} />,
      },
      {
        id: 'count',
        header: 'Count',
        accessorKey: 'count',
        sortingFn: 'basic',
        cell: ({ getValue }) => <Num value={getValue<number>()} />,
      },
      {
        id: 'buy_cost',
        header: 'Buy',
        accessorFn: (r) => orLast(r.buy_cost),
        sortingFn: 'basic',
        cell: ({ row }) => <Gil value={row.original.buy_cost} />,
      },
      {
        id: 'craft_cost',
        header: 'Craft',
        accessorFn: (r) => orLast(r.craft_cost),
        sortingFn: 'basic',
        cell: ({ row }) => <Gil value={row.original.craft_cost} />,
      },
      {
        id: 'best_cost',
        header: 'Best',
        accessorFn: (r) => orLast(r.best_cost),
        sortingFn: 'basic',
        cell: ({ row }) =>
          row.original.method === null ? (
            <span className="rounded bg-destructive/15 px-1.5 py-0.5 text-xs text-destructive">
              short
            </span>
          ) : (
            <span className="inline-flex items-center gap-2">
              <Gil value={row.original.best_cost} />
              <span className="rounded bg-card px-1.5 py-0.5 text-xs text-muted-foreground">
                {row.original.method}
              </span>
            </span>
          ),
      },
      {
        id: 'gil_per_seal',
        header: 'Gil/seal',
        accessorFn: (r) => orLast(r.gil_per_seal),
        sortingFn: 'basic',
        cell: ({ row }) => (
          <span className="font-mono text-sm font-medium tabular-nums text-accent">
            {row.original.gil_per_seal === null
              ? '-'
              : formatNumber(Math.round(row.original.gil_per_seal * 100) / 100)}
          </span>
        ),
      },
    ];
    if (onIgnore || onUnignore) {
      base.push(makeActionsColumn<GcSealsRow>({ ignoredItemIds, onIgnore, onUnignore }));
    }
    return base;
  }, [ignoredItemIds, onIgnore, onUnignore]);

  return (
    <DataTable
      rows={rows}
      columns={columns}
      sortStorageKey="gcSeals"
      defaultSort={DEFAULT_SORT}
      emptyMessage="No expert delivery items to price."
      getRowId={rowId}
      renderExpanded={renderBreakdown}
      rowClassName={(r) => (ignoredItemIds?.includes(r.id) ? 'opacity-50' : '')}
    />
  );
}

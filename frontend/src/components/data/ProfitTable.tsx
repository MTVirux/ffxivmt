import { Link } from 'react-router';
import { type ColumnDef, type SortingState } from '@tanstack/react-table';
import { useMemo } from 'react';
import type { ProfitRow } from '../../api/types';
import { formatGilCompact, formatNumber } from '../../lib/format';
import DataTable, { makeActionsColumn } from './DataTable';

type Props = {
  rows: ProfitRow[];
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

const DEFAULT_SORT: SortingState = [{ id: 'ffmt_score', desc: true }];

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
          <span className="font-mono tabular-nums text-sm">{formatGilCompact(getValue<number>())}</span>
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
      base.push(makeActionsColumn<ProfitRow>({ ignoredItemIds, onIgnore, onUnignore }));
    }
    return base;
  }, [ignoredItemIds, onIgnore, onUnignore]);

  return (
    <DataTable
      rows={rows}
      columns={columns}
      sortStorageKey="itemProfit"
      defaultSort={DEFAULT_SORT}
      emptyMessage="No matching market-board data."
      rowClassName={(r) => (ignoredItemIds?.includes(r.id) ? 'opacity-50' : '')}
    />
  );
}

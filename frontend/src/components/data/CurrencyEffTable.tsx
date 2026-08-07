import { Link } from 'react-router';
import { type ColumnDef, type SortingState } from '@tanstack/react-table';
import { useMemo } from 'react';
import type { CurrencyEfficiencyRow } from '../../api/types';
import { formatGilCompact, formatNumber } from '../../lib/format';
import DataTable, { makeActionsColumn } from './DataTable';

type Props = {
  rows: CurrencyEfficiencyRow[];
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

const DEFAULT_SORT: SortingState = [{ id: 'ffmt_score', desc: true }];

export default function CurrencyEffTable({ rows, ignoredItemIds, onIgnore, onUnignore }: Props) {
  const columns = useMemo<ColumnDef<CurrencyEfficiencyRow>[]>(() => {
    const base: ColumnDef<CurrencyEfficiencyRow>[] = [
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
        id: 'price',
        header: 'Cost',
        accessorKey: 'price',
        sortingFn: 'basic',
        cell: ({ getValue, row }) => (
          <span className="font-mono tabular-nums text-sm text-muted-foreground">
            {formatNumber(getValue<number>())}{' '}
            <span className="text-xs">{row.original.currency_name}</span>
          </span>
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
            {formatNumber(getValue<number>())}/d
          </span>
        ),
      },
      {
        id: 'median_stack_size',
        header: 'Stack',
        accessorKey: 'median_stack_size',
        sortingFn: 'basic',
        cell: ({ getValue }) => (
          <span className="font-mono tabular-nums text-sm text-muted-foreground">
            {formatNumber(getValue<number>())}
          </span>
        ),
      },
      {
        id: 'daily_market_cap',
        header: 'Daily cap',
        accessorKey: 'daily_market_cap',
        sortingFn: 'basic',
        cell: ({ getValue }) => (
          <span className="font-mono tabular-nums text-sm">{formatGilCompact(getValue<number>())}</span>
        ),
      },
      {
        id: 'daily_market_cap_percent',
        header: 'Share',
        accessorKey: 'daily_market_cap_percent',
        sortingFn: 'basic',
        cell: ({ getValue }) => (
          <span className="font-mono tabular-nums text-sm text-muted-foreground">
            {getValue<number>().toFixed(2)}%
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
            {formatNumber(Math.round(getValue<number>() * 100) / 100)}
          </span>
        ),
      },
    ];
    if (onIgnore || onUnignore) {
      base.push(makeActionsColumn<CurrencyEfficiencyRow>({ ignoredItemIds, onIgnore, onUnignore }));
    }
    return base;
  }, [ignoredItemIds, onIgnore, onUnignore]);

  return (
    <DataTable
      rows={rows}
      columns={columns}
      sortStorageKey="currencyEff"
      defaultSort={DEFAULT_SORT}
      emptyMessage="No marketable trade-currency listings."
      rowClassName={(r) => (ignoredItemIds?.includes(r.id) ? 'opacity-50' : '')}
    />
  );
}

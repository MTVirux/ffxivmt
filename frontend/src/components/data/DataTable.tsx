import {
  flexRender,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type FilterFn,
  type SortingState,
} from '@tanstack/react-table';
import { Fragment, useMemo, useState, type ReactNode } from 'react';
import { usePersistedSort } from '../../hooks/usePersistedSort';
import type { TableSortKey } from '../../hooks/useUserPrefs';
import { matchesItemName } from '../../lib/itemFilter';
import EmptyState from '../layout/EmptyState';
import TableSearch from '../form/TableSearch';

type Props<T> = {
  rows: T[];
  columns: ColumnDef<T>[];
  sortStorageKey: TableSortKey;
  defaultSort: SortingState;
  emptyMessage: string;
  /** Field the search box filters on. Defaults to `name`. */
  nameFilterKey?: keyof T;
  rowClassName?: (row: T) => string;
  getRowId?: (row: T) => string;
  /** When set, clicking a row toggles this content in a full-width row beneath it. */
  renderExpanded?: (row: T) => ReactNode;
};

export default function DataTable<T>({
  rows,
  columns,
  sortStorageKey,
  defaultSort,
  emptyMessage,
  nameFilterKey,
  rowClassName,
  getRowId,
  renderExpanded,
}: Props<T>) {
  const [globalFilter, setGlobalFilter] = useState('');
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(() => new Set());
  const toggleExpanded = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      return next;
    });

  const sortableIds = useMemo(
    () => columns.map((c) => c.id).filter((id): id is string => typeof id === 'string'),
    [columns],
  );
  const [sorting, setSorting] = usePersistedSort(sortStorageKey, defaultSort, sortableIds);

  const filterKey = (nameFilterKey ?? 'name') as keyof T;
  const nameFilter = useMemo<FilterFn<T>>(
    () => (row, _columnId, value) =>
      matchesItemName(String(row.original[filterKey] ?? ''), value as string),
    [filterKey],
  );

  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    globalFilterFn: nameFilter,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getRowId: getRowId ? (row) => getRowId(row) : undefined,
  });

  if (rows.length === 0) {
    return <EmptyState>{emptyMessage}</EmptyState>;
  }

  const filteredRows = table.getRowModel().rows;

  return (
    <div>
      <TableSearch
        value={globalFilter}
        onChange={setGlobalFilter}
        resultCount={filteredRows.length}
        totalCount={rows.length}
      />
      {filteredRows.length === 0 ? (
        <EmptyState>No items match “{globalFilter}”.</EmptyState>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-border/60">
          <table className="w-full text-sm">
            <thead className="bg-card/60 text-xs uppercase tracking-widest text-muted-foreground">
              {table.getHeaderGroups().map((hg) => (
                <tr key={hg.id}>
                  {hg.headers.map((h) => {
                    const numeric = h.column.id !== 'name' && h.column.id !== 'actions';
                    const sort = h.column.getIsSorted();
                    return (
                      <th
                        key={h.id}
                        scope="col"
                        className={[
                          'cursor-pointer select-none whitespace-nowrap px-3 py-2 font-medium hover:text-foreground',
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
              {filteredRows.map((row) => {
                const isExpanded = expanded.has(row.id);
                return (
                  <Fragment key={row.id}>
                    <tr
                      onClick={renderExpanded ? () => toggleExpanded(row.id) : undefined}
                      aria-expanded={renderExpanded ? isExpanded : undefined}
                      className={[
                        'border-t border-border/40 hover:bg-card/30',
                        renderExpanded ? 'cursor-pointer' : '',
                        rowClassName?.(row.original) ?? '',
                      ]
                        .filter(Boolean)
                        .join(' ')}
                    >
                      {row.getVisibleCells().map((cell) => {
                        const numeric = cell.column.id !== 'name' && cell.column.id !== 'actions';
                        return (
                          <td
                            key={cell.id}
                            className={[
                              'whitespace-nowrap px-3 py-2',
                              numeric ? 'text-right' : 'text-left',
                            ].join(' ')}
                          >
                            {flexRender(cell.column.columnDef.cell, cell.getContext())}
                          </td>
                        );
                      })}
                    </tr>
                    {isExpanded && renderExpanded ? (
                      <tr className="border-t border-border/20 bg-card/20">
                        <td colSpan={row.getVisibleCells().length} className="px-3 py-3">
                          {renderExpanded(row.original)}
                        </td>
                      </tr>
                    ) : null}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

type ActionsColumnArgs = {
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

// eslint-disable-next-line react-refresh/only-export-components
export function makeActionsColumn<T extends { id: number }>({
  ignoredItemIds,
  onIgnore,
  onUnignore,
}: ActionsColumnArgs): ColumnDef<T> {
  return {
    id: 'actions',
    header: '',
    enableSorting: false,
    cell: ({ row }) => {
      const id = row.original.id;
      const isIgnored = ignoredItemIds?.includes(id) ?? false;
      if (isIgnored && onUnignore) {
        return (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              onUnignore(id);
            }}
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
            onClick={(e) => {
              e.stopPropagation();
              onIgnore(id);
            }}
            className="text-xs text-muted-foreground hover:text-destructive"
            aria-label="Ignore item"
          >
            ×
          </button>
        );
      }
      return null;
    },
  };
}

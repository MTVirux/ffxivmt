import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { Td, Th } from '../../components/data/TableCells';
import TextField from '../../components/form/TextField';
import EmptyState from '../../components/layout/EmptyState';
import QueryBoundary from '../../components/layout/QueryBoundary';
import { useBuyerSearch } from '../../hooks/useBuyerSearch';
import { useItemNames } from '../../hooks/useItemNames';
import { useUserPrefs } from '../../hooks/useUserPrefs';
import { useWorlds } from '../../hooks/useWorlds';
import { relativeTime } from '../../lib/time';
import { buildWorldNameMap } from '../../lib/worlds';
import type { BuyerSearchRow, WorldStructure } from '../../api/types';

const schema = z.object({
  buyerName: z.string().trim().min(1, 'Enter a buyer name'),
});

type FormValues = z.infer<typeof schema>;
type Submission = { buyerName: string; world: string } | null;

export default function BuyerSearchPage() {
  const [prefs, patchPrefs] = useUserPrefs();
  const [submission, setSubmission] = useState<Submission>(null);

  // Kept separate from lastLocation: this picker has an "All worlds" option the
  // tiered Location type can't express.
  const world = prefs.buyerSearchWorld;
  const setWorld = (next: string) => patchPrefs({ buyerSearchWorld: next });

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { buyerName: prefs.toolInputs.buyerSearch },
  });

  const query = useBuyerSearch({
    buyerName: submission?.buyerName ?? '',
    world: submission?.world ?? '',
    enabled: submission !== null,
  });

  const worlds = useWorlds();

  const worldNameMap = useMemo(() => buildWorldNameMap(worlds.data), [worlds.data]);

  const itemIds = useMemo(
    () => (query.data ? query.data.map((row) => row.item_id) : []),
    [query.data],
  );
  const itemNameMap = useItemNames(itemIds);

  const onSubmit = handleSubmit((values) => {
    patchPrefs((prev) => ({
      toolInputs: { ...prev.toolInputs, buyerSearch: values.buyerName },
    }));
    setSubmission({ buyerName: values.buyerName, world });
  });

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">buyer search</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Buyer search</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Look up marketboard purchases by character name.
        </p>
      </header>

      <form
        onSubmit={onSubmit}
        className="flex flex-wrap items-end gap-4 rounded-xl border border-border/60 bg-card/40 p-4"
      >
        <div className="min-w-[16rem] flex-1">
          <TextField
            label="Buyer name"
            placeholder="e.g. Firstname Lastname"
            autoComplete="off"
            error={errors.buyerName?.message}
            {...register('buyerName')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <span className="text-xs uppercase tracking-widest text-muted-foreground">
            Purchase World
          </span>
          <WorldSelect
            value={world}
            onChange={setWorld}
            worlds={worlds.data}
            loading={worlds.isLoading}
          />
        </div>

        <button
          type="submit"
          disabled={query.isFetching}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-foreground transition-colors hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {query.isFetching ? 'Searching…' : 'Search'}
        </button>
      </form>

      <section className="space-y-3">
        {submission === null ? (
          <EmptyState>Enter a buyer name to see their purchase history.</EmptyState>
        ) : (
          <QueryBoundary
            query={query}
            errorText={(query.error as Error)?.message ?? 'Failed to load purchase history.'}
          >
            {(rows) =>
              rows.length === 0 ? (
                <EmptyState>No purchases found.</EmptyState>
              ) : (
                <ResultsTable
                  rows={rows}
                  worldNameMap={worldNameMap}
                  itemNameMap={itemNameMap}
                />
              )
            }
          </QueryBoundary>
        )}
      </section>
    </div>
  );
}

function WorldSelect({
  value,
  onChange,
  worlds,
  loading,
}: {
  value: string;
  onChange: (name: string) => void;
  worlds: WorldStructure | undefined;
  loading: boolean;
}) {
  const groups = useMemo(() => {
    if (!worlds) return [] as { label: string; options: string[] }[];
    return Object.entries(worlds).flatMap(([region, dcs]) =>
      Object.entries(dcs).map(([dc, ws]) => ({
        label: `${region} · ${dc}`,
        options: Object.values(ws).sort((a, b) => a.localeCompare(b)),
      })),
    );
  }, [worlds]);

  return (
    <select
      value={value}
      disabled={loading || !worlds}
      onChange={(e) => onChange(e.target.value)}
      className="rounded-md border border-border/60 bg-card px-3 py-2 text-sm text-foreground transition-colors focus:border-accent focus:outline-none focus:ring-1 focus:ring-accent/50 disabled:cursor-not-allowed disabled:opacity-50"
    >
      <option value="">All worlds</option>
      {groups.map((g) => (
        <optgroup key={g.label} label={g.label}>
          {g.options.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </optgroup>
      ))}
    </select>
  );
}

function ResultsTable({
  rows,
  worldNameMap,
  itemNameMap,
}: {
  rows: BuyerSearchRow[];
  worldNameMap: Map<number, string>;
  itemNameMap: Map<number, string>;
}) {
  const sorted = useMemo(
    () => [...rows].sort((a, b) => Date.parse(b.sale_time) - Date.parse(a.sale_time)),
    [rows],
  );

  return (
    <div className="overflow-hidden rounded-xl border border-border/60">
      <table className="w-full text-sm">
        <thead className="bg-card/60 text-xs uppercase tracking-widest text-muted-foreground">
          <tr>
            <Th>Item</Th>
            <Th>Purchase World</Th>
            <Th>Buyer</Th>
            <Th>When</Th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((row, i) => (
            <tr
              key={`${row.item_id}-${row.world_id}-${row.sale_time}-${i}`}
              className="border-t border-border/40 even:bg-card/20"
            >
              <Td>
                <Link to={`/item/${row.item_id}`} className="text-accent hover:underline">
                  {itemNameMap.get(row.item_id) ?? `#${row.item_id}`}
                </Link>
              </Td>
              <Td>{worldNameMap.get(row.world_id) ?? String(row.world_id)}</Td>
              <Td muted>{row.buyer_name}</Td>
              <Td>
                <span title={row.sale_time}>{relativeTime(row.sale_time)}</span>
              </Td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}


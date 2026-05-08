import { zodResolver } from '@hookform/resolvers/zod';
import { useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import ProfitTable from '../../components/data/ProfitTable';
import TextField from '../../components/form/TextField';
import TieredLocationSelect from '../../components/form/TieredLocationSelect';
import { useItemProductProfit } from '../../hooks/useItemProductProfit';
import type { Location } from '../../api/types';

const schema = z.object({
  searchTerm: z.string().trim().min(1, 'Enter an item name'),
});

type FormValues = z.infer<typeof schema>;

type Submission = { searchTerm: string; location: string } | null;

export default function ItemProfitPage() {
  const [location, setLocation] = useState<Location | undefined>(undefined);
  const [submission, setSubmission] = useState<Submission>(null);
  const [locationError, setLocationError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const query = useItemProductProfit({
    searchTerm: submission?.searchTerm ?? '',
    location: submission?.location ?? '',
    enabled: submission !== null,
  });

  const onSubmit = handleSubmit((values) => {
    if (!location) {
      setLocationError('Pick a location');
      return;
    }
    setLocationError(null);
    setSubmission({ searchTerm: values.searchTerm, location: location.name });
  });

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">profit solver</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Item product profit</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Pick a material; we walk its Garland recipe partials, fetch live market-board data
          across the chosen location, and rank everything by{' '}
          <span className="font-mono">min_price × velocity</span>.
        </p>
      </header>

      <form
        onSubmit={onSubmit}
        className="flex flex-wrap items-end gap-4 rounded-xl border border-border/60 bg-card/40 p-4"
      >
        <div className="min-w-[16rem] flex-1">
          <TextField
            label="Item name"
            placeholder="e.g. Mythril Ingot"
            autoComplete="off"
            error={errors.searchTerm?.message}
            {...register('searchTerm')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <span className="text-xs uppercase tracking-widest text-muted-foreground">Location</span>
          <TieredLocationSelect value={location} onChange={setLocation} />
          {locationError && (
            <span className="text-xs text-destructive">{locationError}</span>
          )}
        </div>

        <button
          type="submit"
          disabled={query.isFetching}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-foreground transition-colors hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {query.isFetching ? 'Calculating…' : 'Calculate'}
        </button>
      </form>

      <section className="space-y-3">
        {submission === null ? (
          <Hint>Enter an item and pick a location to see the recipe-partial breakdown.</Hint>
        ) : query.isLoading ? (
          <div className="h-64 animate-pulse rounded-xl bg-card/40" />
        ) : query.isError ? (
          <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
            {(query.error as Error)?.message ?? 'Failed to compute profit.'}
          </div>
        ) : query.data ? (
          <>
            <header className="flex items-baseline justify-between text-xs text-muted-foreground">
              <span>
                <span className="uppercase tracking-widest">resolved</span>{' '}
                <span className="font-mono text-foreground">{query.data.item_name}</span>
                <span className="ml-2 text-muted-foreground">on {query.data.location}</span>
              </span>
              <span className="font-mono">{query.data.request_id.slice(0, 8)}</span>
            </header>
            <ProfitTable rows={query.data.data} />
          </>
        ) : null}
      </section>
    </div>
  );
}

function Hint({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
      {children}
    </div>
  );
}

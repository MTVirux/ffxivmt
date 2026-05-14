import { zodResolver } from '@hookform/resolvers/zod';
import { useState, useCallback, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import CurrencyEffTable from '../../components/data/CurrencyEffTable';
import TextField from '../../components/form/TextField';
import TieredLocationSelect from '../../components/form/TieredLocationSelect';
import { useCurrencyEfficiency } from '../../hooks/useCurrencyEfficiency';
import { useUserPrefs } from '../../hooks/useUserPrefs';
import { formatGil } from '../../lib/format';
import type { Location } from '../../api/types';

const schema = z.object({
  searchTerm: z.string().trim().min(1, 'Enter a currency name'),
});

type FormValues = z.infer<typeof schema>;

type Submission = { searchTerm: string; location: string } | null;

export default function CurrencyEffPage() {
  const [location, setLocation] = useState<Location | undefined>(undefined);
  const [submission, setSubmission] = useState<Submission>(null);
  const [locationError, setLocationError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema) });

  const query = useCurrencyEfficiency({
    searchTerm: submission?.searchTerm ?? '',
    location: submission?.location ?? '',
    enabled: submission !== null,
  });

  const [prefs, patchPrefs] = useUserPrefs();
  const [showHidden, setShowHidden] = useState(false);

  const allRows = query.data?.status ? query.data.data : [];
  const visibleRows = showHidden
    ? allRows
    : allRows.filter((r) => !prefs.ignoredItemIds.includes(r.id));

  const handleIgnore = useCallback(
    (id: number) => patchPrefs({ ignoredItemIds: [...prefs.ignoredItemIds, id] }),
    [patchPrefs, prefs.ignoredItemIds],
  );
  const handleUnignore = useCallback(
    (id: number) => patchPrefs({ ignoredItemIds: prefs.ignoredItemIds.filter((x) => x !== id) }),
    [patchPrefs, prefs.ignoredItemIds],
  );

  const onSubmit = handleSubmit((values) => {
    if (!location) {
      setLocationError('Pick a location');
      return;
    }
    setLocationError(null);
    setSubmission({ searchTerm: values.searchTerm, location: location.name });
  });

  const totalCap = visibleRows.reduce((s, r) => s + r.daily_market_cap, 0);

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">currency</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">Currency efficiency</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          Pick a currency or token. We pull every item it buys from Garland, fetch live
          market-board data, and rank by{' '}
          <span className="font-mono">(min_price × velocity / cost) × market share</span>.
        </p>
      </header>

      <form
        onSubmit={onSubmit}
        className="flex flex-wrap items-end gap-4 rounded-xl border border-border/60 bg-card/40 p-4"
      >
        <div className="min-w-[16rem] flex-1">
          <TextField
            label="Currency"
            placeholder="e.g. Allagan Tomestone of Causality"
            autoComplete="off"
            error={errors.searchTerm?.message}
            {...register('searchTerm')}
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <span className="text-xs uppercase tracking-widest text-muted-foreground">Location</span>
          <TieredLocationSelect value={location} onChange={setLocation} />
          {locationError && <span className="text-xs text-destructive">{locationError}</span>}
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
          <Hint>Enter a currency and pick a location to see the efficiency breakdown.</Hint>
        ) : query.isLoading ? (
          <div className="h-64 animate-pulse rounded-xl bg-card/40" />
        ) : query.isError ? (
          <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
            {(query.error as Error)?.message ?? 'Failed to compute efficiency.'}
          </div>
        ) : query.data?.status ? (
          <>
            <header className="flex flex-wrap items-baseline justify-between gap-3 text-xs text-muted-foreground">
              <span>
                <span className="uppercase tracking-widest">resolved</span>{' '}
                <span className="font-mono text-foreground">{query.data.item_name}</span>
                <span className="ml-2">on {query.data.location}</span>
              </span>
              <div className="flex items-center gap-4">
                <span className="font-mono">
                  Daily market cap {formatGil(totalCap)} · {query.data.request_id.slice(0, 8)}
                </span>
                <label className="flex cursor-pointer items-center gap-2 text-xs text-muted-foreground hover:text-foreground">
                  <input
                    type="checkbox"
                    checked={showHidden}
                    onChange={(e) => setShowHidden(e.target.checked)}
                    className="size-4 rounded border-border/60 bg-card accent-[var(--color-accent)]"
                  />
                  Show hidden items
                </label>
              </div>
            </header>
            <CurrencyEffTable
              rows={visibleRows}
              ignoredItemIds={showHidden ? prefs.ignoredItemIds : undefined}
              onIgnore={handleIgnore}
              onUnignore={showHidden ? handleUnignore : undefined}
            />
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

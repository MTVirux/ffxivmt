import { zodResolver } from '@hookform/resolvers/zod';
import { useCallback, useMemo, useState, type ReactNode } from 'react';
import { useForm, type UseFormReturn } from 'react-hook-form';
import { z } from 'zod';
import CheckboxToggle from '../form/CheckboxToggle';
import TieredLocationSelect from '../form/TieredLocationSelect';
import EmptyState from './EmptyState';
import QueryBoundary from './QueryBoundary';
import { useIgnoredItems } from '../../hooks/useIgnoredItems';
import { useToolCalculator } from '../../hooks/useToolCalculator';
import { patchPrefs, useUserPrefs } from '../../hooks/useUserPrefs';
import type { Location } from '../../api/types';

export type ToolFormValues = { searchTerm: string };

/** Props the calculator tables take for the ignore/unhide column. */
export type ToolTableActions = {
  ignoredItemIds?: number[];
  onIgnore?: (id: number) => void;
  onUnignore?: (id: number) => void;
};

type Props<TRow extends { id: number }> = {
  eyebrow: string;
  title: string;
  blurb: ReactNode;
  /** Persists the last search term and keys the query cache. */
  toolKey: 'currencyEff' | 'itemProfit';
  endpoint: string;
  searchTermMessage: string;
  idleHint: string;
  errorFallback: string;
  field: (form: UseFormReturn<ToolFormValues>) => ReactNode;
  renderTable: (rows: TRow[], actions: ToolTableActions) => ReactNode;
  /** Rendered before the request id in the result header. */
  extraMeta?: (rows: TRow[]) => ReactNode;
};

type Submission = { searchTerm: string; location: string } | null;

export default function ToolPage<TRow extends { id: number }>({
  eyebrow,
  title,
  blurb,
  toolKey,
  endpoint,
  searchTermMessage,
  idleHint,
  errorFallback,
  field,
  renderTable,
  extraMeta,
}: Props<TRow>) {
  const [prefs] = useUserPrefs();
  const [submission, setSubmission] = useState<Submission>(null);
  const [locationError, setLocationError] = useState<string | null>(null);
  const { ids: ignoredItemIds, ignore, unignore } = useIgnoredItems();

  const location = prefs.lastLocation;
  const setLocation = useCallback((next: Location) => patchPrefs({ lastLocation: next }), []);
  const showHidden = prefs.showHidden;

  const schema = useMemo(
    () => z.object({ searchTerm: z.string().trim().min(1, searchTermMessage) }),
    [searchTermMessage],
  );

  const form = useForm<ToolFormValues>({
    resolver: zodResolver(schema),
    defaultValues: { searchTerm: prefs.toolInputs[toolKey] },
  });

  const query = useToolCalculator<TRow>(endpoint, toolKey, {
    searchTerm: submission?.searchTerm ?? '',
    location: submission?.location ?? '',
    enabled: submission !== null,
  });

  const onSubmit = form.handleSubmit((values) => {
    if (!location) {
      setLocationError('Pick a location');
      return;
    }
    setLocationError(null);
    patchPrefs((prev) => ({
      toolInputs: { ...prev.toolInputs, [toolKey]: values.searchTerm },
    }));
    setSubmission({ searchTerm: values.searchTerm, location: location.name });
  });

  return (
    <div className="space-y-8">
      <header>
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">{eyebrow}</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight">{title}</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">{blurb}</p>
      </header>

      <form
        onSubmit={onSubmit}
        className="flex flex-wrap items-end gap-4 rounded-xl border border-border/60 bg-card/40 p-4"
      >
        <div className="min-w-[16rem] flex-1">{field(form)}</div>

        <div className="flex flex-col gap-1.5">
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
          <EmptyState>{idleHint}</EmptyState>
        ) : (
          <QueryBoundary
            query={query}
            errorText={(query.error as Error)?.message ?? errorFallback}
          >
            {(data) => {
              if (!data.status) return null;
              const rows = showHidden
                ? data.data
                : data.data.filter((r) => !ignoredItemIds.includes(r.id));
              return (
                <>
                  <header className="flex flex-wrap items-baseline justify-between gap-3 text-xs text-muted-foreground">
                    <span>
                      <span className="uppercase tracking-widest">resolved</span>{' '}
                      <span className="font-mono text-foreground">{data.item_name}</span>
                      <span className="ml-2">on {data.location}</span>
                    </span>
                    <div className="flex items-center gap-4">
                      <span className="font-mono">
                        {extraMeta?.(rows)}
                        {data.request_id.slice(0, 8)}
                      </span>
                      <CheckboxToggle
                        size="xs"
                        label="Show hidden items"
                        checked={showHidden}
                        onChange={(v) => patchPrefs({ showHidden: v })}
                      />
                    </div>
                  </header>
                  {renderTable(rows, {
                    ignoredItemIds: showHidden ? ignoredItemIds : undefined,
                    onIgnore: ignore,
                    onUnignore: showHidden ? unignore : undefined,
                  })}
                </>
              );
            }}
          </QueryBoundary>
        )}
      </section>
    </div>
  );
}

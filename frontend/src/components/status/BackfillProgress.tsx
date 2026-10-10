import { Td, Th } from '../data/TableCells';
import QueryBoundary from '../layout/QueryBoundary';
import { useBackfillProgress } from '../../hooks/useBackfillProgress';
import { formatAgo, formatCount, formatDay } from '../../lib/statusFormat';

export default function BackfillProgress() {
  const query = useBackfillProgress();

  return (
    <section>
      <h2 className="text-sm font-medium uppercase tracking-widest text-muted-foreground">
        History backfill
      </h2>
      <p className="mt-2 text-sm text-muted-foreground">
        How far back sales history has been imported, per region. Each region&apos;s catalogue is
        split into buckets that walk back independently.
      </p>
      <div className="mt-4">
        <QueryBoundary
          query={query}
          skeletonClassName="h-40 animate-pulse rounded-xl bg-card/40"
          errorText="Backfill progress could not be loaded."
        >
          {(data) =>
            data.available ? (
              <div className="overflow-x-auto rounded-xl border border-border/60 bg-card">
                <table className="w-full whitespace-nowrap text-sm">
                  <thead className="bg-card/60 text-xs uppercase tracking-widest text-muted-foreground">
                    <tr>
                      <Th>Region</Th>
                      <Th align="right">Buckets done</Th>
                      <Th>Reached back to</Th>
                      <Th>Slowest bucket</Th>
                      <Th>Last advanced</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.regions.map((r) => (
                      <tr key={r.region} className="border-t border-border/40 even:bg-card/20">
                        <Td>{r.region}</Td>
                        <Td align="right" mono>
                          {formatCount(r.buckets_complete)} / {formatCount(r.buckets_total)}
                        </Td>
                        <Td mono>{formatDay(r.reached_back_to)}</Td>
                        <Td mono>{formatDay(r.slowest_bucket_at)}</Td>
                        <Td muted>{formatAgo(r.last_advanced_at)}</Td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">
                Backfill progress is unavailable right now.
              </p>
            )
          }
        </QueryBoundary>
      </div>
    </section>
  );
}

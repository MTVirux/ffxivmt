import type { ReactNode } from 'react';

export default function EmptyState({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
      {children}
    </div>
  );
}

import type { UseQueryResult } from '@tanstack/react-query';
import type { ReactNode } from 'react';

export const QUERY_SKELETON_CLASS = 'h-64 animate-pulse rounded-xl bg-card/40';

type Props<T> = {
  query: UseQueryResult<T>;
  skeletonClassName?: string;
  errorText: string;
  children: (data: T) => ReactNode;
};

export default function QueryBoundary<T>({
  query,
  skeletonClassName = QUERY_SKELETON_CLASS,
  errorText,
  children,
}: Props<T>) {
  if (query.isLoading) {
    return <div className={skeletonClassName} />;
  }
  if (query.isError) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
        {errorText}
      </div>
    );
  }
  if (query.data === undefined) return null;
  return <>{children(query.data)}</>;
}

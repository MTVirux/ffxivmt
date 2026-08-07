import type { ReactNode } from 'react';

export function Th({ children, align }: { children: ReactNode; align?: 'right' }) {
  return (
    <th
      scope="col"
      className={`px-3 py-2 font-medium ${align === 'right' ? 'text-right' : 'text-left'}`}
    >
      {children}
    </th>
  );
}

export function Td({
  children,
  align,
  mono,
  muted,
}: {
  children: ReactNode;
  align?: 'right';
  mono?: boolean;
  muted?: boolean;
}) {
  return (
    <td
      className={[
        'px-3 py-2',
        align === 'right' ? 'text-right' : 'text-left',
        mono ? 'font-mono tabular-nums' : '',
        muted ? 'text-muted-foreground' : '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {children}
    </td>
  );
}

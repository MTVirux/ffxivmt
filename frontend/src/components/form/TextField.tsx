import { forwardRef, type InputHTMLAttributes, type ReactNode } from 'react';

type Props = {
  label: string;
  hint?: ReactNode;
  error?: string;
} & InputHTMLAttributes<HTMLInputElement>;

const TextField = forwardRef<HTMLInputElement, Props>(function TextField(
  { label, hint, error, className, id, ...rest },
  ref,
) {
  const inputId = id ?? `tf-${label.replace(/\s+/g, '-').toLowerCase()}`;
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-xs uppercase tracking-widest text-muted-foreground">
        {label}
      </label>
      <input
        ref={ref}
        id={inputId}
        className={[
          'rounded-md border bg-card px-3 py-2 text-sm text-foreground',
          'transition-colors focus:outline-none focus:ring-1',
          error
            ? 'border-destructive/60 focus:border-destructive focus:ring-destructive/40'
            : 'border-border/60 focus:border-accent focus:ring-accent/50',
          className,
        ]
          .filter(Boolean)
          .join(' ')}
        {...rest}
      />
      {error ? (
        <span className="text-xs text-destructive">{error}</span>
      ) : hint ? (
        <span className="text-xs text-muted-foreground">{hint}</span>
      ) : null}
    </div>
  );
});

export default TextField;

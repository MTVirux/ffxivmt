import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import {
  CURRENCY_GROUPS,
  filterCurrencyGroups,
  isKnownCurrency,
} from '../../lib/currencies';

type Props = {
  label: string;
  value: string;
  onChange: (next: string) => void;
  placeholder?: string;
  error?: string;
};

// Combobox over the known currencies, grouped by type. Free text still submits,
// so anything missing from the catalogue stays reachable.
export default function CurrencySelect({
  label,
  value,
  onChange,
  placeholder,
  error,
}: Props) {
  const [open, setOpen] = useState(false);
  const [highlight, setHighlight] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const inputId = useId();
  const listId = useId();

  // A committed pick would otherwise filter the list down to itself, leaving no
  // way to browse to another currency without clearing the field first.
  const groups = useMemo(
    () => (isKnownCurrency(value) ? CURRENCY_GROUPS : filterCurrencyGroups(CURRENCY_GROUPS, value)),
    [value],
  );
  const flat = useMemo(() => groups.flatMap((g) => g.currencies), [groups]);
  const optionId = (id: number) => `${listId}-opt-${id}`;

  useEffect(() => {
    const selected = flat.findIndex((c) => c.name === value);
    setHighlight(selected >= 0 ? selected : 0);
  }, [value, flat]);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (e: PointerEvent) => {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    const active = flat[highlight];
    if (active) {
      document.getElementById(optionId(active.id))?.scrollIntoView({ block: 'nearest' });
    }
  });

  const commit = (name: string) => {
    onChange(name);
    setOpen(false);
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }
      if (flat.length === 0) return;
      const step = e.key === 'ArrowDown' ? 1 : -1;
      setHighlight((h) => (h + step + flat.length) % flat.length);
      return;
    }
    if (e.key === 'Enter') {
      // Only swallow Enter when it picks a suggestion; otherwise submit the form.
      if (open && flat[highlight]) {
        e.preventDefault();
        commit(flat[highlight].name);
      }
      return;
    }
    if (e.key === 'Escape') {
      if (open) {
        e.preventDefault();
        setOpen(false);
      }
      return;
    }
    if (e.key === 'Tab') setOpen(false);
  };

  return (
    <div ref={rootRef} className="relative flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-xs uppercase tracking-widest text-muted-foreground">
        {label}
      </label>

      <div
        className={[
          'flex items-center gap-2 rounded-md border bg-card px-3 py-2 transition-colors',
          error
            ? 'border-destructive/60 focus-within:border-destructive focus-within:ring-1 focus-within:ring-destructive/40'
            : 'border-border/60 focus-within:border-accent focus-within:ring-1 focus-within:ring-accent/50',
        ].join(' ')}
      >
        <input
          id={inputId}
          type="text"
          role="combobox"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={open && flat[highlight] ? optionId(flat[highlight].id) : undefined}
          autoComplete="off"
          value={value}
          placeholder={placeholder}
          onChange={(e) => {
            onChange(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
          className="min-w-0 flex-1 bg-transparent text-sm text-foreground placeholder:text-muted-foreground focus:outline-none"
        />
        <button
          type="button"
          tabIndex={-1}
          aria-label={open ? 'Hide currencies' : 'Show currencies'}
          onClick={() => setOpen((o) => !o)}
          className="shrink-0 select-none text-xs text-muted-foreground hover:text-foreground"
        >
          {open ? '▴' : '▾'}
        </button>
      </div>

      {open && (
        <div
          id={listId}
          role="listbox"
          aria-label="Currencies"
          className="absolute top-full z-20 mt-1 max-h-72 w-full overflow-y-auto rounded-md border border-border/60 bg-card py-1 shadow-lg"
        >
          {groups.length === 0 ? (
            <p className="px-3 py-2 text-xs text-muted-foreground">
              No known currency matches - press Calculate to search anyway.
            </p>
          ) : (
            groups.map((group) => (
              <div key={group.category} role="group" aria-label={group.category}>
                <p className="sticky top-0 bg-card px-3 py-1 font-mono text-[0.65rem] uppercase tracking-widest text-accent">
                  {group.category}
                </p>
                {group.currencies.map((currency) => {
                  const active = flat[highlight]?.id === currency.id;
                  return (
                    <div
                      key={currency.id}
                      id={optionId(currency.id)}
                      role="option"
                      aria-selected={currency.name === value}
                      onMouseDown={(e) => {
                        e.preventDefault();
                        commit(currency.name);
                      }}
                      onMouseEnter={() => setHighlight(flat.findIndex((c) => c.id === currency.id))}
                      className={[
                        'cursor-pointer px-3 py-1.5 text-sm',
                        active ? 'bg-accent/20 text-foreground' : 'text-muted-foreground',
                      ].join(' ')}
                    >
                      {currency.name}
                    </div>
                  );
                })}
              </div>
            ))
          )}
        </div>
      )}

      {error && <span className="text-xs text-destructive">{error}</span>}
    </div>
  );
}

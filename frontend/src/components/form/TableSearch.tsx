type Props = {
  value: string;
  onChange: (next: string) => void;
  placeholder?: string;
  resultCount?: number;
  totalCount?: number;
};

export default function TableSearch({
  value,
  onChange,
  placeholder = 'Search items…',
  resultCount,
  totalCount,
}: Props) {
  const showCount =
    value.trim() !== '' && resultCount !== undefined && totalCount !== undefined;
  return (
    <div className="mb-3 flex items-center gap-2 rounded-md border border-border/60 bg-card px-3 py-2 transition-colors focus-within:border-accent focus-within:ring-1 focus-within:ring-accent/50">
      <span aria-hidden="true" className="select-none text-muted-foreground">
        ⌕
      </span>
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label="Search items"
        className="min-w-0 flex-1 bg-transparent text-sm text-foreground placeholder:text-muted-foreground focus:outline-none"
      />
      {showCount && (
        <span className="shrink-0 font-mono text-xs text-muted-foreground">
          {resultCount} of {totalCount}
        </span>
      )}
      {value !== '' && (
        <button
          type="button"
          onClick={() => onChange('')}
          aria-label="Clear search"
          className="shrink-0 rounded border border-border/60 px-1.5 text-xs text-muted-foreground hover:text-foreground"
        >
          ×
        </button>
      )}
    </div>
  );
}

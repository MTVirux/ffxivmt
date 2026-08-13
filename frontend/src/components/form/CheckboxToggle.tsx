type Props = {
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
  size?: 'sm' | 'xs';
};

export default function CheckboxToggle({ label, checked, onChange, size = 'sm' }: Props) {
  return (
    <label
      className={
        size === 'xs'
          ? 'flex cursor-pointer items-center gap-2 text-xs text-muted-foreground hover:text-foreground'
          : 'flex cursor-pointer items-center gap-2 text-sm text-muted-foreground hover:text-foreground'
      }
    >
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="size-4 rounded border-border/60 bg-card accent-[var(--color-accent)]"
      />
      {label}
    </label>
  );
}

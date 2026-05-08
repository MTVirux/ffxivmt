type Props = {
  title: string;
  subtitle: string;
  body: string;
};

export default function Placeholder({ title, subtitle, body }: Props) {
  return (
    <div className="rounded-xl border border-dashed border-border/80 bg-card/40 p-10">
      <p className="font-mono text-xs uppercase tracking-widest text-muted-foreground">
        {subtitle}
      </p>
      <h1 className="mt-2 text-3xl font-semibold tracking-tight">{title}</h1>
      <p className="mt-4 max-w-2xl text-muted-foreground">{body}</p>
    </div>
  );
}

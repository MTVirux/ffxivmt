import { xivApiIconUrl } from '../../lib/iconUrl';

type Props = {
  icon: number;
  alt: string;
  size?: number;
  className?: string;
};

export default function ItemIcon({ icon, alt, size = 40, className }: Props) {
  const url = xivApiIconUrl(icon);
  const dim = { width: size, height: size };

  if (!url) {
    return (
      <div
        aria-hidden="true"
        className={['rounded-md bg-muted/50', className].filter(Boolean).join(' ')}
        style={dim}
      />
    );
  }

  return (
    <img
      src={url}
      alt={alt}
      loading="lazy"
      decoding="async"
      className={['rounded-md bg-muted/40 object-contain', className].filter(Boolean).join(' ')}
      style={dim}
    />
  );
}

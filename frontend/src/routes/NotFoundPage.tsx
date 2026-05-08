import { Link } from 'react-router-dom';

export default function NotFoundPage() {
  return (
    <div className="flex flex-col items-start gap-4">
      <p className="font-mono text-sm text-muted-foreground">404</p>
      <h1 className="text-3xl font-semibold tracking-tight">Page not found</h1>
      <Link to="/" className="text-sm text-accent hover:underline">
        Back to home
      </Link>
    </div>
  );
}

import { Outlet } from 'react-router-dom';
import Navbar from './Navbar';

export default function Shell() {
  return (
    <div className="flex min-h-full flex-col">
      <Navbar />
      <main className="w-full flex-1 px-6 py-10">
        <Outlet />
      </main>
      <footer className="border-t border-border/60 py-6">
        <div className="w-full px-6 text-sm text-muted-foreground">
          FFXIV Market Tools - Not affiliated with Square Enix.
        </div>
      </footer>
    </div>
  );
}

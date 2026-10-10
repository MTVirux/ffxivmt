import { Suspense } from 'react';
import { Link, Outlet } from 'react-router';
import Navbar from './Navbar';
import { QUERY_SKELETON_CLASS } from './QueryBoundary';

function GithubIcon() {
  return (
    <svg
      viewBox="0 0 16 16"
      width="16"
      height="16"
      fill="currentColor"
      aria-hidden="true"
    >
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8Z" />
    </svg>
  );
}

export default function Shell() {
  return (
    <div className="flex min-h-full flex-col">
      <Navbar />
      <main className="mx-auto w-full lg:w-[70%] flex-1 px-6 py-10">
        <Suspense fallback={<div className={QUERY_SKELETON_CLASS} />}>
          <Outlet />
        </Suspense>
      </main>
      <footer className="border-t border-border/60 py-6">
        <div className="mx-auto grid w-full lg:w-[70%] grid-cols-1 justify-items-center items-center gap-6 px-6 text-center text-sm text-muted-foreground sm:grid-cols-[1fr_auto_1fr] sm:justify-items-stretch sm:text-left">
          <span>FFXIV Market Tools - Not affiliated with Square Enix.</span>
          <Link to="/status" className="transition-colors hover:text-foreground sm:justify-self-center">
            Status
          </Link>
          <a
            href="https://github.com/MTVirux/ffxivmt"
            target="_blank"
            rel="noreferrer noopener"
            className="flex items-center gap-2 transition-colors hover:text-foreground sm:justify-self-end"
            aria-label="FFXIV Market Tools on GitHub"
          >
            <GithubIcon />
            <span className="hidden sm:inline">GitHub</span>
          </a>
        </div>
      </footer>
    </div>
  );
}

import { NavLink } from 'react-router-dom';
import { navItems } from '../../config/navigation';

export default function Navbar() {
  return (
    <header className="sticky top-0 z-10 border-b border-border/60 bg-background/80 backdrop-blur">
      <div className="mx-auto flex w-full lg:w-[70%] items-center justify-between gap-6 px-6 py-4">
        <NavLink
          to="/"
          className="flex items-baseline gap-1 font-mono text-lg tracking-tight"
        >
          <span className="text-accent">ff</span>
          <span className="text-foreground">mt</span>
          <span className="ml-2 hidden text-xs font-normal text-muted-foreground sm:inline">
            market tools
          </span>
        </NavLink>

        <nav className="flex items-center gap-1 text-sm">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                [
                  'rounded-md px-3 py-1.5 transition-colors',
                  isActive
                    ? 'bg-card text-foreground'
                    : 'text-muted-foreground hover:bg-card/60 hover:text-foreground',
                ].join(' ')
              }
            >
              {item.navLabel}
            </NavLink>
          ))}
        </nav>
      </div>
    </header>
  );
}

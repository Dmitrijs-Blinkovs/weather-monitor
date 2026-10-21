import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { lazy, Suspense, useState } from 'react';
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation } from 'react-router';
import { ErrorBoundary } from './components/ErrorBoundary';

// Pages are split into their own chunks; the charting library only loads with the chart page.
const ChartPage = lazy(() => import('./features/chart/ChartPage').then((module) => ({ default: module.ChartPage })));
const FetchLogPage = lazy(() =>
  import('./features/fetch-log/FetchLogPage').then((module) => ({ default: module.FetchLogPage })),
);

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
    },
  });
}

export function App() {
  const [queryClient] = useState(createQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppShell />
      </BrowserRouter>
    </QueryClientProvider>
  );
}

function AppShell() {
  const location = useLocation();

  return (
    <div className="app">
      <header className="app-header">
        <div className="app-header__inner">
          <a className="app-header__brand" href="/">
            <img src="/favicon.svg" alt="" width={22} height={22} />
            Weather Monitor
          </a>
          <nav className="app-nav" aria-label="Main">
            <NavLink to="/temperature" className="app-nav__link">
              Temperature
            </NavLink>
            <NavLink to="/fetch-log" className="app-nav__link">
              Fetch log
            </NavLink>
          </nav>
        </div>
      </header>

      <main className="app-main">
        {/* Keyed by path, so navigating to another page recovers from an error on the previous one. */}
        <ErrorBoundary key={location.pathname}>
          <Suspense fallback={<p className="muted">Loading…</p>}>
            <Routes>
              <Route path="/" element={<Navigate to="/temperature" replace />} />
              <Route path="/temperature" element={<ChartPage />} />
              <Route path="/fetch-log" element={<FetchLogPage />} />
              <Route path="*" element={<Navigate to="/temperature" replace />} />
            </Routes>
          </Suspense>
        </ErrorBoundary>
      </main>

      <footer className="app-footer">
        <p className="app-footer__inner">Designed and built by Dmitrijs Bļinkovs</p>
      </footer>
    </div>
  );
}

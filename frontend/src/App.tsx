import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { lazy } from 'react';
import { BrowserRouter, Route, Routes } from 'react-router';
import Shell from './components/layout/Shell';
import HomePage from './routes/HomePage';
import NotFoundPage from './routes/NotFoundPage';

// Split out so recharts (ItemPage) and react-virtual (GilfluxPage) stay out of the entry chunk.
const ItemPage = lazy(() => import('./routes/ItemPage'));
const GilfluxPage = lazy(() => import('./routes/GilfluxPage'));
const ItemProfitPage = lazy(() => import('./routes/tools/ItemProfitPage'));
const CurrencyEffPage = lazy(() => import('./routes/tools/CurrencyEffPage'));
const BuyerSearchPage = lazy(() => import('./routes/tools/BuyerSearchPage'));

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Gilflux server cache is 20s (GilfluxOptions.RankingCacheSeconds); align so we
      // don't refetch faster than the backend can produce fresh values.
      staleTime: 20_000,
      refetchOnWindowFocus: false,
    },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route element={<Shell />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/item/:id" element={<ItemPage />} />
            <Route path="/gilflux" element={<GilfluxPage />} />
            <Route path="/tools/item-product-profit-calculator" element={<ItemProfitPage />} />
            <Route path="/tools/currency-efficiency-calculator" element={<CurrencyEffPage />} />
            <Route path="/tools/buyer-search" element={<BuyerSearchPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

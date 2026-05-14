import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import Shell from './components/layout/Shell';
import HomePage from './routes/HomePage';
import ItemPage from './routes/ItemPage';
import GilfluxPage from './routes/GilfluxPage';
import NotFoundPage from './routes/NotFoundPage';
import ItemProfitPage from './routes/tools/ItemProfitPage';
import CurrencyEffPage from './routes/tools/CurrencyEffPage';
import BuyerSearchPage from './routes/tools/BuyerSearchPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Gilflux server cache is 20s (config/cache_timers.php); align so we don't
      // refetch faster than the backend can produce fresh values.
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

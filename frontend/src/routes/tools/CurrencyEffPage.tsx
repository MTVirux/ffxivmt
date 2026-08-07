import { Controller } from 'react-hook-form';
import CurrencyEffTable from '../../components/data/CurrencyEffTable';
import CurrencySelect from '../../components/form/CurrencySelect';
import ToolPage from '../../components/layout/ToolPage';
import { formatGil } from '../../lib/format';
import type { CurrencyEfficiencyRow } from '../../api/types';

export default function CurrencyEffPage() {
  return (
    <ToolPage<CurrencyEfficiencyRow>
      eyebrow="currency"
      title="Currency efficiency"
      blurb={
        <>
          The best way to spend your currency.
          <span className="font-mono">(min_price × velocity / cost) × market share</span>.
        </>
      }
      toolKey="currencyEff"
      endpoint="/tools/currency_efficiency_calculator"
      searchTermMessage="Enter a currency name"
      idleHint="Enter a currency and pick a location to see the efficiency breakdown."
      errorFallback="Failed to compute efficiency."
      field={(form) => (
        <Controller
          name="searchTerm"
          control={form.control}
          render={({ field }) => (
            <CurrencySelect
              label="Currency"
              placeholder="Pick a currency or type a name"
              value={field.value}
              onChange={field.onChange}
              error={form.formState.errors.searchTerm?.message}
            />
          )}
        />
      )}
      extraMeta={(rows) => (
        <>Daily market cap {formatGil(rows.reduce((s, r) => s + r.daily_market_cap, 0))} · </>
      )}
      renderTable={(rows, actions) => <CurrencyEffTable rows={rows} {...actions} />}
    />
  );
}

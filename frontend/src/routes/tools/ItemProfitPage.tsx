import ProfitTable from '../../components/data/ProfitTable';
import TextField from '../../components/form/TextField';
import ToolPage from '../../components/layout/ToolPage';
import type { ProfitRow } from '../../api/types';

export default function ItemProfitPage() {
  return (
    <ToolPage<ProfitRow>
      eyebrow="profit solver"
      title="Item product profit"
      blurb={
        <>
          What should I craft with this item?
          <span className="font-mono">min_price × velocity</span>.
        </>
      }
      toolKey="itemProfit"
      endpoint="/tools/item_product_profit_calculator"
      searchTermMessage="Enter an item name"
      idleHint="Enter an item and pick a location to see the recipe-partial breakdown."
      errorFallback="Failed to compute profit."
      field={(form) => (
        <TextField
          label="Item name"
          placeholder="e.g. Mythril Ingot"
          autoComplete="off"
          error={form.formState.errors.searchTerm?.message}
          {...form.register('searchTerm')}
        />
      )}
      renderTable={(rows, actions) => <ProfitTable rows={rows} {...actions} />}
    />
  );
}

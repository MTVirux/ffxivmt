using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaItemStore(IScyllaSession scylla) : IItemStore
{
    private const string CqlGetById = "SELECT id, name, marketable, craftable, icon_image FROM items WHERE id = ?";
    private const string CqlGetAllNames = "SELECT id, name FROM items";
    private const string CqlGetAllIds = "SELECT id FROM items";

    private const string CqlGetMarketableIds = "SELECT item_id FROM marketable_items WHERE bucket = 0";
    private const string CqlGetCraftableIds  = "SELECT item_id FROM craftable_items  WHERE bucket = 0";
    private const string CqlGetScripIds      = "SELECT item_id FROM scrip_items      WHERE bucket = 0";

    private const string CqlUpsertItem = """
        INSERT INTO items (
            id, name, description,
            can_be_hq, always_collectible, stack_size, item_level, icon_image,
            rarity, filter_group, item_ui_category, item_search_category, equip_slot_category,
            "unique", untradable, disposable, dyable, aetherial_reductible,
            materia_slot_count, advanced_melding,
            craftable, marketable, from_scrips
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private const string CqlUpdateMarketable = "UPDATE items SET marketable = ? WHERE id = ?";
    private const string CqlUpdateCraftable  = "UPDATE items SET craftable  = ? WHERE id = ?";

    private const string CqlInsertMarketableMember = "INSERT INTO marketable_items (bucket, item_id) VALUES (0, ?)";
    private const string CqlDeleteMarketableMember = "DELETE FROM marketable_items WHERE bucket = 0 AND item_id = ?";

    private const string CqlInsertCraftableMember = "INSERT INTO craftable_items (bucket, item_id) VALUES (0, ?)";
    private const string CqlDeleteCraftableMember = "DELETE FROM craftable_items WHERE bucket = 0 AND item_id = ?";

    private const string CqlInsertScripMember = "INSERT INTO scrip_items (bucket, item_id) VALUES (0, ?)";
    private const string CqlDeleteScripMember = "DELETE FROM scrip_items WHERE bucket = 0 AND item_id = ?";

    public async Task<Item?> GetAsync(int id, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetById, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(id)).ConfigureAwait(false);
        var row = rows.FirstOrDefault();
        return row is null ? null : MapRow(row);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetAllNamesAsync(CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetAllNames, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind()).ConfigureAwait(false);
        var result = new Dictionary<int, string>();
        foreach (var row in rows)
        {
            result[row.GetValue<int>("id")] = row.GetValue<string>("name") ?? string.Empty;
        }
        return result;
    }

    public Task<IReadOnlyList<int>> GetAllIdsAsync(CancellationToken ct = default) =>
        FetchIdsAsync(CqlGetAllIds, columnName: "id", ct);

    public Task<IReadOnlyList<int>> GetMarketableIdsAsync(CancellationToken ct = default) =>
        FetchIdsAsync(CqlGetMarketableIds, columnName: "item_id", ct);

    public Task<IReadOnlyList<int>> GetCraftableIdsAsync(CancellationToken ct = default) =>
        FetchIdsAsync(CqlGetCraftableIds, columnName: "item_id", ct);

    public async Task UpsertAsync(ItemUpsert item, CancellationToken ct = default)
    {
        // ItemUpsert seeds craftable/marketable/from_scrips as false. Maintain the lookup
        // tables accordingly: a fresh items reseed clears membership, and later stages
        // (UpdateGarland / UpdateMarketability / scrip flagging) re-add membership.
        var upsertStmt = await scylla.PrepareAsync(CqlUpsertItem, ct).ConfigureAwait(false);
        var dropMarketStmt = await scylla.PrepareAsync(CqlDeleteMarketableMember, ct).ConfigureAwait(false);
        var dropCraftStmt = await scylla.PrepareAsync(CqlDeleteCraftableMember, ct).ConfigureAwait(false);
        var dropScripStmt = await scylla.PrepareAsync(CqlDeleteScripMember, ct).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(upsertStmt.Bind(
            item.Id, item.Name, item.Description,
            item.CanBeHq, item.AlwaysCollectible, item.StackSize, item.ItemLevel, item.IconImage,
            item.Rarity, item.FilterGroup, item.ItemUiCategory, item.ItemSearchCategory, item.EquipSlotCategory,
            item.Unique, item.Untradable, item.Disposable, item.Dyable, item.AetherialReductible,
            item.MateriaSlotCount, item.AdvancedMelding,
            false, false, false))
            .ConfigureAwait(false);

        await Task.WhenAll(
            scylla.Session.ExecuteAsync(dropMarketStmt.Bind(item.Id)),
            scylla.Session.ExecuteAsync(dropCraftStmt.Bind(item.Id)),
            scylla.Session.ExecuteAsync(dropScripStmt.Bind(item.Id))
        ).ConfigureAwait(false);
    }

    public async Task UpdateMarketableAsync(int id, bool marketable, CancellationToken ct = default)
    {
        // Prepare both statements upfront so a single Bind/Execute failure can't strand
        // the companion-table maintenance in a half-applied state.
        var memberCql = marketable ? CqlInsertMarketableMember : CqlDeleteMarketableMember;
        var stmtTask = scylla.PrepareAsync(CqlUpdateMarketable, ct);
        var memberStmtTask = scylla.PrepareAsync(memberCql, ct);
        await Task.WhenAll(stmtTask, memberStmtTask).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(stmtTask.Result.Bind(marketable, id)).ConfigureAwait(false);
        await scylla.Session.ExecuteAsync(memberStmtTask.Result.Bind(id)).ConfigureAwait(false);
    }

    public async Task UpdateCraftableAsync(int id, bool craftable, CancellationToken ct = default)
    {
        var memberCql = craftable ? CqlInsertCraftableMember : CqlDeleteCraftableMember;
        var stmtTask = scylla.PrepareAsync(CqlUpdateCraftable, ct);
        var memberStmtTask = scylla.PrepareAsync(memberCql, ct);
        await Task.WhenAll(stmtTask, memberStmtTask).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(stmtTask.Result.Bind(craftable, id)).ConfigureAwait(false);
        await scylla.Session.ExecuteAsync(memberStmtTask.Result.Bind(id)).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<int>> FetchIdsAsync(string cql, string columnName, CancellationToken ct)
    {
        var stmt = await scylla.PrepareAsync(cql, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind()).ConfigureAwait(false);
        var result = new List<int>();
        foreach (var row in rows)
        {
            result.Add(row.GetValue<int>(columnName));
        }
        return result;
    }

    private static Item MapRow(Row row) => new(
        row.GetValue<int>("id"),
        row.GetValue<string>("name") ?? string.Empty,
        SafeBool(row, "marketable"),
        SafeBool(row, "craftable"),
        SafeInt(row, "icon_image"));

    private static int SafeInt(Row row, string column) =>
        row.IsNull(column) ? 0 : row.GetValue<int>(column);

    private static bool SafeBool(Row row, string column) =>
        !row.IsNull(column) && row.GetValue<bool>(column);
}

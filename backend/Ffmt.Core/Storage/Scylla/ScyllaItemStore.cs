using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaItemStore(IScyllaSession scylla) : IItemStore
{
    private const string CqlGetById      = "SELECT id, name, marketable, craftable, icon_image FROM items WHERE id = ?";
    private const string CqlGetAllNames  = "SELECT id, name FROM items";
    private const string CqlGetAllIds    = "SELECT id FROM items";

    private const string CqlGetSetMembers    = "SELECT item_id FROM item_sets WHERE set_name = ?";
    private const string CqlInsertSetMember  = "INSERT INTO item_sets (set_name, item_id) VALUES (?, ?)";
    private const string CqlDeleteSetMember  = "DELETE FROM item_sets WHERE set_name = ? AND item_id = ?";

    private const string MarketableSet = "marketable";
    private const string CraftableSet  = "craftable";
    private const string ScripSet      = "scrip";

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
        FetchIdsFromTableAsync(CqlGetAllIds, columnName: "id", ct);

    public Task<IReadOnlyList<int>> GetMarketableIdsAsync(CancellationToken ct = default) =>
        FetchSetMembersAsync(MarketableSet, ct);

    public Task<IReadOnlyList<int>> GetCraftableIdsAsync(CancellationToken ct = default) =>
        FetchSetMembersAsync(CraftableSet, ct);

    public async Task UpsertAsync(ItemUpsert item, CancellationToken ct = default)
    {
        var upsertStmt  = await scylla.PrepareAsync(CqlUpsertItem, ct).ConfigureAwait(false);
        var dropSetStmt = await scylla.PrepareAsync(CqlDeleteSetMember, ct).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(upsertStmt.Bind(
            item.Id, item.Name, item.Description,
            item.CanBeHq, item.AlwaysCollectible, item.StackSize, item.ItemLevel, item.IconImage,
            item.Rarity, item.FilterGroup, item.ItemUiCategory, item.ItemSearchCategory, item.EquipSlotCategory,
            item.Unique, item.Untradable, item.Disposable, item.Dyable, item.AetherialReductible,
            item.MateriaSlotCount, item.AdvancedMelding,
            false, false, false))
            .ConfigureAwait(false);

        await Task.WhenAll(
            scylla.Session.ExecuteAsync(dropSetStmt.Bind(MarketableSet, item.Id)),
            scylla.Session.ExecuteAsync(dropSetStmt.Bind(CraftableSet,  item.Id)),
            scylla.Session.ExecuteAsync(dropSetStmt.Bind(ScripSet,      item.Id))
        ).ConfigureAwait(false);
    }

    public async Task UpdateMarketableAsync(int id, bool marketable, CancellationToken ct = default)
    {
        var memberCql = marketable ? CqlInsertSetMember : CqlDeleteSetMember;
        var stmtTask = scylla.PrepareAsync(CqlUpdateMarketable, ct);
        var memberStmtTask = scylla.PrepareAsync(memberCql, ct);
        await Task.WhenAll(stmtTask, memberStmtTask).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(stmtTask.Result.Bind(marketable, id)).ConfigureAwait(false);
        await scylla.Session.ExecuteAsync(memberStmtTask.Result.Bind(MarketableSet, id)).ConfigureAwait(false);
    }

    public async Task UpdateCraftableAsync(int id, bool craftable, CancellationToken ct = default)
    {
        var memberCql = craftable ? CqlInsertSetMember : CqlDeleteSetMember;
        var stmtTask = scylla.PrepareAsync(CqlUpdateCraftable, ct);
        var memberStmtTask = scylla.PrepareAsync(memberCql, ct);
        await Task.WhenAll(stmtTask, memberStmtTask).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(stmtTask.Result.Bind(craftable, id)).ConfigureAwait(false);
        await scylla.Session.ExecuteAsync(memberStmtTask.Result.Bind(CraftableSet, id)).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<int>> FetchSetMembersAsync(string setName, CancellationToken ct)
    {
        var stmt = await scylla.PrepareAsync(CqlGetSetMembers, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(setName)).ConfigureAwait(false);
        var result = new List<int>();
        foreach (var row in rows)
        {
            result.Add(row.GetValue<int>("item_id"));
        }
        return result;
    }

    private async Task<IReadOnlyList<int>> FetchIdsFromTableAsync(string cql, string columnName, CancellationToken ct)
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

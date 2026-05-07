namespace Ffmt.Core.External;

/// <summary>
/// Garland Tools item-craft lookup. Used by the <c>updatedb</c> CLI to flip the
/// <c>craftable</c> flag on items that have a recipe.
/// </summary>
public interface IGarlandClient
{
    /// <summary>
    /// Returns one entry per id in <paramref name="ids"/>; <c>HasCraft</c> is <c>true</c>
    /// when Garland reports any non-empty craft recipe for that item.
    /// </summary>
    Task<IReadOnlyList<GarlandItemFlags>> GetItemBatchAsync(IReadOnlyList<int> ids, CancellationToken ct = default);
}

public sealed record GarlandItemFlags(int Id, bool HasCraft);

using System.Diagnostics.CodeAnalysis;

namespace Ffmt.Core.Quarantine;

public interface IPriceBaselineProvider
{
    // Blocks only on the first call; later calls return at once and reload in the background when stale.
    Task EnsureLoadedAsync(CancellationToken ct = default);

    Task ReloadAsync(CancellationToken ct = default);

    bool TryGet(int itemId, string region, bool hq, [MaybeNullWhen(false)] out PriceBaseline baseline);
}

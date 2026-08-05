using System.Diagnostics.CodeAnalysis;

namespace Ffmt.Core.Quarantine;

public interface IPriceBaselineProvider
{
    /// <summary>Blocks on the first call while the snapshot loads; afterwards returns immediately,
    /// kicking a background reload when the snapshot has aged past the refresh interval.</summary>
    Task EnsureLoadedAsync(CancellationToken ct = default);

    Task ReloadAsync(CancellationToken ct = default);

    bool TryGet(int itemId, string region, bool hq, [MaybeNullWhen(false)] out PriceBaseline baseline);
}

using System.Diagnostics.CodeAnalysis;
using Ffmt.Core.Quarantine;

namespace Ffmt.Tests.Fakes;

internal sealed class StubBaselines : IPriceBaselineProvider
{
    public Dictionary<(int ItemId, string Region, bool Hq), PriceBaseline> Rows { get; } = [];

    public Task EnsureLoadedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;

    public bool TryGet(int itemId, string region, bool hq, [MaybeNullWhen(false)] out PriceBaseline baseline) =>
        Rows.TryGetValue((itemId, region, hq), out baseline);
}

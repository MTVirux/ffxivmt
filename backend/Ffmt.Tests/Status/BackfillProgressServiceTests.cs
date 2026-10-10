using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Status;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Ffmt.Tests.Status;

public sealed class BackfillProgressServiceTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IBackfillStateStore _store = Substitute.For<IBackfillStateStore>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    private BackfillProgressService CreateService(params string[] regions) =>
        new(_store, Options.Create(new UniversalisOptions { RegionsToUse = regions }), _cache,
            TimeProvider.System, NullLogger<BackfillProgressService>.Instance);

    private static BackfillBucketProgress Bucket(
        int bucket, DateTimeOffset? earliest, bool complete = false, DateTimeOffset? written = null) =>
        new(bucket, earliest, complete, written);

    private void StoreReturns(string region, params BackfillBucketProgress[] buckets) =>
        _store.GetBucketProgressAsync(region, BackfillLoops.Historical, Arg.Any<CancellationToken>())
            .Returns(buckets);

    [Fact]
    public void Summarize_counts_buckets_and_spans_their_pointers()
    {
        var progress = BackfillProgressService.Summarize("Europe",
        [
            Bucket(0, T0.AddDays(-10), complete: true, written: T0.AddHours(-5)),
            Bucket(1, T0.AddDays(-40), written: T0.AddHours(-1)),
            Bucket(2, T0.AddDays(-300), written: T0.AddHours(-3)),
        ]);

        progress.Should().Be(new RegionBackfillProgress(
            "Europe",
            BucketsTotal: 3,
            BucketsComplete: 1,
            ReachedBackTo: T0.AddDays(-300).ToUnixTimeSeconds(),
            SlowestBucketAt: T0.AddDays(-40).ToUnixTimeSeconds(),
            LastAdvancedAt: T0.AddHours(-1).ToUnixTimeSeconds()));
    }

    [Fact]
    public void Summarize_skips_buckets_without_a_pointer()
    {
        var progress = BackfillProgressService.Summarize("Japan",
        [
            Bucket(0, null),
            Bucket(1, T0.AddDays(-20)),
        ]);

        progress.Should().Be(new RegionBackfillProgress(
            "Japan", 2, 0, T0.AddDays(-20).ToUnixTimeSeconds(), T0.AddDays(-20).ToUnixTimeSeconds(), null));
    }

    [Fact]
    public void Summarize_has_no_slowest_bucket_once_every_bucket_is_complete()
    {
        var progress = BackfillProgressService.Summarize("Europe",
        [
            Bucket(0, T0.AddDays(-100), complete: true, written: T0.AddHours(-2)),
            Bucket(1, null, complete: true),
        ]);

        progress.Should().Be(new RegionBackfillProgress(
            "Europe", 2, 2, T0.AddDays(-100).ToUnixTimeSeconds(), null, T0.AddHours(-2).ToUnixTimeSeconds()));
    }

    [Fact]
    public void Summarize_an_empty_region_is_zeros_and_nulls()
    {
        BackfillProgressService.Summarize("Oceania", [])
            .Should().Be(new RegionBackfillProgress("Oceania", 0, 0, null, null, null));
    }

    [Fact]
    public async Task Reports_regions_in_configured_order()
    {
        var japan = new TaskCompletionSource<IReadOnlyList<BackfillBucketProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.GetBucketProgressAsync("Japan", BackfillLoops.Historical, Arg.Any<CancellationToken>()).Returns(japan.Task);
        StoreReturns("Europe", Bucket(0, T0), Bucket(1, T0));
        StoreReturns("Oceania");

        var pending = CreateService("Japan", "Europe", "Oceania").GetAsync(CancellationToken.None);
        japan.SetResult([Bucket(0, T0), Bucket(1, T0), Bucket(2, T0)]);
        var response = await pending;

        response.Available.Should().BeTrue();
        response.Regions.Select(r => (r.Region, r.BucketsTotal))
            .Should().Equal(("Japan", 3), ("Europe", 2), ("Oceania", 0));
    }

    [Fact]
    public async Task Store_failure_reports_unavailable()
    {
        StoreReturns("Europe", Bucket(0, T0));
        _store.GetBucketProgressAsync("Japan", BackfillLoops.Historical, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("scylla down"));

        var response = await CreateService("Europe", "Japan").GetAsync(CancellationToken.None);

        response.Available.Should().BeFalse();
        response.Regions.Should().BeEmpty();
        response.GeneratedAt.Should().BePositive();
    }

    [Fact]
    public async Task Caches_the_response()
    {
        StoreReturns("Europe", Bucket(0, T0));
        var service = CreateService("Europe");

        await service.GetAsync(CancellationToken.None);
        await service.GetAsync(CancellationToken.None);

        await _store.Received(1).GetBucketProgressAsync("Europe", BackfillLoops.Historical, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_caller_does_not_cancel_the_shared_build()
    {
        var europe = new TaskCompletionSource<IReadOnlyList<BackfillBucketProgress>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.GetBucketProgressAsync("Europe", BackfillLoops.Historical, Arg.Any<CancellationToken>())
            .Returns(ci => europe.Task.WaitAsync(ci.Arg<CancellationToken>()));
        using var cts = new CancellationTokenSource();

        var cancelled = CreateService("Europe").GetAsync(cts.Token);
        await cts.CancelAsync();
        await FluentActions.Awaiting(() => cancelled).Should().ThrowAsync<OperationCanceledException>();

        europe.SetResult([Bucket(0, T0)]);
        (await CreateService("Europe").GetAsync(CancellationToken.None)).Available.Should().BeTrue();
        await _store.Received(1).GetBucketProgressAsync("Europe", BackfillLoops.Historical, Arg.Any<CancellationToken>());
    }
}

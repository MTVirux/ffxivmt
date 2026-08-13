using System.Diagnostics;

using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class TokenBucketTests
{
    /// <summary>Stopwatch-scale ticks, advanced by hand so refill maths needs no real waiting.</summary>
    private sealed class FakeClock
    {
        private long _ticks;

        public long Now() => _ticks;

        public void Advance(TimeSpan by) => _ticks += (long)(by.TotalSeconds * Stopwatch.Frequency);
    }

    /// <summary>A consume that must wait for a refill blocks until its token arrives, so a short
    /// cancellation is how the test asserts "this one would have waited".</summary>
    private static async Task<bool> ConsumesWithoutWaiting(TokenBucket bucket)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        try
        {
            await bucket.ConsumeAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [Fact]
    public async Task Hands_out_the_whole_burst_before_making_anyone_wait()
    {
        var clock = new FakeClock();
        using var bucket = new TokenBucket(capacity: 3, refillRate: 2, clock.Now);

        for (var i = 0; i < 3; i++)
            (await ConsumesWithoutWaiting(bucket)).Should().BeTrue("the burst covers the first {0} requests", 3);

        (await ConsumesWithoutWaiting(bucket)).Should().BeFalse("the burst is spent and no time has passed");
    }

    [Fact]
    public async Task Refills_at_the_configured_rate()
    {
        var clock = new FakeClock();
        using var bucket = new TokenBucket(capacity: 5, refillRate: 10, clock.Now);

        for (var i = 0; i < 5; i++)
            await ConsumesWithoutWaiting(bucket);

        clock.Advance(TimeSpan.FromMilliseconds(300));

        for (var i = 0; i < 3; i++)
            (await ConsumesWithoutWaiting(bucket)).Should().BeTrue("300ms at 10/s refills exactly three tokens");

        (await ConsumesWithoutWaiting(bucket)).Should().BeFalse("the fourth token has not been refilled yet");
    }

    [Fact]
    public async Task Never_banks_more_than_the_burst_capacity()
    {
        var clock = new FakeClock();
        using var bucket = new TokenBucket(capacity: 2, refillRate: 10, clock.Now);

        for (var i = 0; i < 2; i++)
            await ConsumesWithoutWaiting(bucket);

        clock.Advance(TimeSpan.FromSeconds(10));

        for (var i = 0; i < 2; i++)
            (await ConsumesWithoutWaiting(bucket)).Should().BeTrue("the bucket refilled back to its capacity");

        (await ConsumesWithoutWaiting(bucket)).Should().BeFalse(
            "an idle bucket must not bank the 100 tokens those 10 seconds were worth");
    }

    [Fact]
    public async Task Starts_full_so_the_first_request_never_waits()
    {
        var clock = new FakeClock();
        using var bucket = new TokenBucket(capacity: 1, refillRate: 1, clock.Now);

        (await ConsumesWithoutWaiting(bucket)).Should().BeTrue();
    }
}

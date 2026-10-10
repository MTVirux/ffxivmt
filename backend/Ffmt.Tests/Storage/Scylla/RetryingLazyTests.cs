using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class RetryingLazyTests
{
    [Fact]
    public void A_successful_value_is_created_once()
    {
        var calls = 0;
        var lazy = new RetryingLazy<object>(() =>
        {
            calls++;
            return new object();
        });

        var first = lazy.Value;
        var second = lazy.Value;

        second.Should().BeSameAs(first);
        calls.Should().Be(1);
        lazy.IsValueCreated.Should().BeTrue();
    }

    [Fact]
    public void A_failed_factory_is_retried_on_the_next_read()
    {
        var calls = 0;
        var lazy = new RetryingLazy<string>(() => ++calls == 1
            ? throw new TimeoutException("connect timed out")
            : "connected");

        lazy.Invoking(l => l.Value).Should().Throw<TimeoutException>();
        lazy.IsValueCreated.Should().BeFalse();
        lazy.Value.Should().Be("connected");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task Concurrent_readers_share_one_attempt()
    {
        var calls = 0;
        using var gate = new ManualResetEventSlim();
        var lazy = new RetryingLazy<object>(() =>
        {
            Interlocked.Increment(ref calls);
            gate.Wait();
            return new object();
        });

        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() => lazy.Value)).ToList();
        await Task.Delay(100);
        gate.Set();
        await Task.WhenAll(readers);

        calls.Should().Be(1);
    }
}

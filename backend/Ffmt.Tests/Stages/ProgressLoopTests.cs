using Ffmt.Cli.Stages;
using Microsoft.Extensions.Logging;

namespace Ffmt.Tests.Stages;

public sealed class ProgressLoopTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages) { Messages.Add(formatter(state, exception)); }
        }
    }

    [Fact]
    public async Task Runs_the_action_once_per_row()
    {
        var seen = new List<int>();
        var rows = Enumerable.Range(1, 250).ToList();

        await ProgressLoop.RunAsync(rows, new CapturingLogger(), "Widgets",
            (row, _) => { lock (seen) { seen.Add(row); } return Task.CompletedTask; },
            concurrency: 8, CancellationToken.None);

        seen.Should().BeEquivalentTo(rows);
    }

    [Fact]
    public async Task Never_exceeds_the_concurrency_bound()
    {
        var inFlight = 0;
        var peak = 0;

        await ProgressLoop.RunAsync(Enumerable.Range(1, 500).ToList(), new CapturingLogger(), "Widgets",
            async (_, _) =>
            {
                var now = Interlocked.Increment(ref inFlight);
                InterlockedMax(ref peak, now);
                await Task.Yield();
                Interlocked.Decrement(ref inFlight);
            },
            concurrency: 4, CancellationToken.None);

        peak.Should().BeLessThanOrEqualTo(4).And.BeGreaterThan(1);
    }

    [Fact]
    public async Task Logs_progress_every_thousand_rows_and_a_final_total()
    {
        var log = new CapturingLogger();

        await ProgressLoop.RunAsync(Enumerable.Range(1, 2500).ToList(), log, "Upserted items into Scylla",
            (_, _) => Task.CompletedTask, concurrency: 16, CancellationToken.None);

        log.Messages.Should().HaveCount(3);
        log.Messages.Should().Contain("Upserted items into Scylla: 1000/2500.");
        log.Messages.Should().Contain("Upserted items into Scylla: 2000/2500.");
        log.Messages[^1].Should().Be("Upserted items into Scylla: 2500 total.");
    }

    [Fact]
    public async Task Logs_only_the_total_when_there_is_nothing_to_do()
    {
        var log = new CapturingLogger();

        await ProgressLoop.RunAsync(Array.Empty<int>(), log, "Widgets",
            (_, _) => Task.CompletedTask, concurrency: 16, CancellationToken.None);

        log.Messages.Should().Equal("Widgets: 0 total.");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
            if (value <= seen) return;
        }
        while (Interlocked.CompareExchange(ref target, value, seen) != seen);
    }
}

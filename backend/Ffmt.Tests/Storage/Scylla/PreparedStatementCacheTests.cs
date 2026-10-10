using System.Runtime.CompilerServices;
using Cassandra;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class PreparedStatementCacheTests
{
    private static PreparedStatement NewStatement() =>
        (PreparedStatement)RuntimeHelpers.GetUninitializedObject(typeof(PreparedStatement));

    [Fact]
    public async Task Each_cql_text_is_prepared_once()
    {
        var calls = new List<string>();
        var cache = new PreparedStatementCache(cql =>
        {
            calls.Add(cql);
            return Task.FromResult(NewStatement());
        });

        var first = await cache.GetAsync("SELECT a");
        var second = await cache.GetAsync("SELECT a");
        await cache.GetAsync("SELECT b");

        second.Should().BeSameAs(first);
        calls.Should().Equal("SELECT a", "SELECT b");
    }

    [Fact]
    public async Task Concurrent_callers_share_one_prepare()
    {
        var calls = 0;
        var gate = new TaskCompletionSource<PreparedStatement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new PreparedStatementCache(_ =>
        {
            Interlocked.Increment(ref calls);
            return gate.Task;
        });

        var waiting = Enumerable.Range(0, 8).Select(_ => cache.GetAsync("SELECT a")).ToList();
        gate.SetResult(NewStatement());
        await Task.WhenAll(waiting);

        calls.Should().Be(1);
    }

    [Fact]
    public async Task A_failed_prepare_is_retried_on_the_next_call()
    {
        var calls = 0;
        var cache = new PreparedStatementCache(_ => ++calls == 1
            ? Task.FromException<PreparedStatement>(new InvalidQueryException("unconfigured table mannequin_sales"))
            : Task.FromResult(NewStatement()));

        await cache.Invoking(c => c.GetAsync("SELECT a")).Should().ThrowAsync<InvalidQueryException>();
        (await cache.GetAsync("SELECT a")).Should().NotBeNull();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task A_synchronous_throw_is_retried_too()
    {
        var calls = 0;
        var cache = new PreparedStatementCache(_ => ++calls == 1
            ? throw new InvalidOperationException("not connected")
            : Task.FromResult(NewStatement()));

        await cache.Invoking(c => c.GetAsync("SELECT a")).Should().ThrowAsync<InvalidOperationException>();
        (await cache.GetAsync("SELECT a")).Should().NotBeNull();
        calls.Should().Be(2);
    }
}

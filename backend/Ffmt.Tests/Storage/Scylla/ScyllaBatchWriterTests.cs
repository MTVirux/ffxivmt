using Cassandra;
using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class ScyllaBatchWriterTests
{
    private static (IScyllaSession Scylla, List<BatchStatement> Executed) NewSession()
    {
        var executed = new List<BatchStatement>();
        var session = Substitute.For<ISession>();
        session.ExecuteAsync(Arg.Do<IStatement>(s => executed.Add((BatchStatement)s)))
            .Returns(_ => Task.FromResult(new RowSet()));

        var scylla = Substitute.For<IScyllaSession>();
        scylla.Session.Returns(session);
        return (scylla, executed);
    }

    private static Task WriteAsync(IScyllaSession scylla, IEnumerable<int> rows, Action<BatchStatement, int>? bind = null) =>
        ScyllaBatchWriter.ExecuteBatchedAsync(
            scylla, rows, bind ?? ((_, _) => { }), op: null, CancellationToken.None);

    [Fact]
    public async Task Exactly_one_full_batch_is_a_single_execute()
    {
        var (scylla, executed) = NewSession();

        await WriteAsync(scylla, Enumerable.Range(0, ScyllaBatchWriter.BatchRows));

        executed.Should().HaveCount(1);
    }

    [Fact]
    public async Task One_row_past_the_batch_size_flushes_a_second_batch()
    {
        var (scylla, executed) = NewSession();

        await WriteAsync(scylla, Enumerable.Range(0, ScyllaBatchWriter.BatchRows + 1));

        executed.Should().HaveCount(2, "the trailing partial batch is flushed after the loop");
    }

    [Fact]
    public async Task Empty_input_executes_nothing()
    {
        var (scylla, executed) = NewSession();
        var bindCalls = 0;

        await WriteAsync(scylla, Array.Empty<int>(), (_, _) => bindCalls++);

        executed.Should().BeEmpty();
        bindCalls.Should().Be(0);
    }

    [Fact]
    public async Task Bind_is_called_once_per_row()
    {
        var (scylla, _) = NewSession();
        var bound = new List<int>();

        await WriteAsync(scylla, Enumerable.Range(0, 5), (_, row) => bound.Add(row));

        bound.Should().Equal(0, 1, 2, 3, 4);
    }

    [Fact]
    public async Task Batches_are_unlogged_at_local_one()
    {
        var (scylla, executed) = NewSession();

        await WriteAsync(scylla, Enumerable.Range(0, 1));

        executed[0].BatchType.Should().Be(BatchType.Unlogged);
        executed[0].ConsistencyLevel.Should().Be(ConsistencyLevel.LocalOne);
    }

    [Fact]
    public async Task A_custom_batch_size_changes_where_the_flush_lands()
    {
        var (scylla, executed) = NewSession();

        await ScyllaBatchWriter.ExecuteBatchedAsync(
            scylla, Enumerable.Range(0, 5), (_, _) => { }, op: null, CancellationToken.None, batchRows: 2);

        executed.Should().HaveCount(3);
    }
}

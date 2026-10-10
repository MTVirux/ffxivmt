using Cassandra;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class SaleStoreMannequinCqlTests
{
    private static Sale NewSale(bool onMannequin) =>
        new(2, 21, "Alisaie", false, onMannequin, 3, 100, new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    private static ScyllaSaleStore NewStore(IScyllaSession session) =>
        new(session, NullLogger<ScyllaSaleStore>.Instance);

    [Fact]
    public async Task AddBatchAsync_prepares_mannequin_inserts_only_for_a_batch_that_has_one()
    {
        var (session, captured) = CapturingScyllaSession.New();
        var store = NewStore(session);

        try { await store.AddBatchAsync([NewSale(onMannequin: false)]); } catch { }
        captured.Should().NotContain(c => c.Contains("mannequin_sales"));

        try { await store.AddBatchAsync([NewSale(onMannequin: true)]); } catch { }
        captured.Should().Contain(c => c.Contains("INSERT INTO mannequin_sales") && c.Contains("unit_price"));
        captured.Should().Contain(c => c.Contains("INSERT INTO mannequin_sales_days"));
    }

    [Fact]
    public async Task AddBatchAsync_keeps_writing_sales_when_the_mannequin_tables_are_missing()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var cql = ci.Arg<string>();
            captured.Add(cql);
            return cql.Contains("mannequin_sales", StringComparison.Ordinal)
                ? Task.FromException<PreparedStatement>(new InvalidQueryException("unconfigured table mannequin_sales"))
                : Task.FromResult<PreparedStatement>(null!);
        });
        var store = NewStore(session);

        // The null PreparedStatement fails the sales bind - reaching it proves the mannequin failure was swallowed.
        await store.Invoking(s => s.AddBatchAsync([NewSale(onMannequin: true)]))
            .Should().ThrowAsync<NullReferenceException>();
        var attempts = captured.Count(c => c.Contains("INSERT INTO mannequin_sales", StringComparison.Ordinal));

        await store.Invoking(s => s.AddBatchAsync([NewSale(onMannequin: true)]))
            .Should().ThrowAsync<NullReferenceException>();

        attempts.Should().Be(1);
        captured.Count(c => c.Contains("INSERT INTO mannequin_sales", StringComparison.Ordinal))
            .Should().Be(attempts, "a missing table is retried after a back-off, not on every batch");
    }

    [Fact]
    public async Task DeleteExactAsync_removes_the_mannequin_copy_of_a_mannequin_sale()
    {
        var (session, captured) = CapturingScyllaSession.New();

        try { await NewStore(session).DeleteExactAsync([NewSale(onMannequin: true)]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("DELETE FROM mannequin_sales") &&
            c.Contains("sale_time = ?") &&
            c.Contains("buyer_name = ?"));
    }

    [Fact]
    public async Task DeleteExactAsync_leaves_the_mannequin_table_alone_for_ordinary_sales()
    {
        var (session, captured) = CapturingScyllaSession.New();

        try { await NewStore(session).DeleteExactAsync([NewSale(onMannequin: false)]); } catch { }

        captured.Should().NotContain(c => c.Contains("mannequin"));
    }
}

using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class QuarantineStoreCqlTests
{
    private static (ScyllaQuarantineStore Store, List<string> Captured) NewStore()
    {
        var (session, captured) = CapturingScyllaSession.New();
        return (new ScyllaQuarantineStore(session, NullLogger<ScyllaQuarantineStore>.Instance), captured);
    }

    [Fact]
    public async Task AddBatchAsync_inserts_into_sales_quarantine_with_the_audit_columns()
    {
        var (store, captured) = NewStore();
        var sale = new Sale(2, 21, "Alisaie", false, false, 1, 999_999_999,
            new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

        try
        {
            await store.AddBatchAsync([new QuarantinedSale(
                sale, QuarantineReasons.UnitPriceDeviation, 1_000_000, DateTimeOffset.UnixEpoch)]);
        }
        catch { }

        captured.Should().Contain(c =>
            c.Contains("INSERT INTO sales_quarantine") &&
            c.Contains("reason") &&
            c.Contains("baseline_median") &&
            c.Contains("quarantined_at"));
        captured.Should().NotContain(c => c.Contains("sales_by_buyer"),
            "quarantined sales must not be reachable from /search_buyer");
    }

    [Fact]
    public async Task AddBatchAsync_is_a_no_op_for_an_empty_batch()
    {
        var (store, captured) = NewStore();

        await store.AddBatchAsync([]);

        captured.Should().BeEmpty();
    }
}

using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ffmt.Tests.Quarantine;

public sealed class FilteringSaleStoreTests
{
    private static readonly Sale Good = new(1, 21, "Alisaie", false, false, 1, 500, DateTimeOffset.UnixEpoch);
    private static readonly Sale Bad = new(2, 21, "Alphinaud", false, false, 1, 999_999_999, DateTimeOffset.UnixEpoch);

    private sealed class CapturingInner : ISaleWriter
    {
        public List<Sale> Written { get; } = [];

        public Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
        {
            Written.AddRange(sales);
            return Task.FromResult(new SaleBatchResult(sales.Count, 0d));
        }
    }

    private sealed class CapturingQuarantine : IQuarantineStore
    {
        public List<QuarantinedSale> Written { get; } = [];
        public Task AddBatchAsync(IReadOnlyList<QuarantinedSale> sales, CancellationToken ct = default)
        {
            Written.AddRange(sales);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingQuarantine : IQuarantineStore
    {
        public Task AddBatchAsync(IReadOnlyList<QuarantinedSale> sales, CancellationToken ct = default) =>
            throw new InvalidOperationException("scylla is down");
    }

    private static FilteringSaleStore NewStore(ISaleWriter inner, IQuarantineStore quarantine, QuarantineOptions opts) =>
        new(inner, new StubAnomalyFilter(), quarantine, Options.Create(opts),
            NullLogger<FilteringSaleStore>.Instance);

    [Fact]
    public async Task Armed_writes_only_accepted_sales_and_records_the_rest()
    {
        var inner = new CapturingInner();
        var quarantine = new CapturingQuarantine();
        var store = NewStore(inner, quarantine, new QuarantineOptions { ShadowMode = false });

        await store.AddBatchAsync([Good, Bad]);

        inner.Written.Should().ContainSingle().Which.Should().Be(Good);
        quarantine.Written.Should().ContainSingle().Which.Sale.Should().Be(Bad);
    }

    [Fact]
    public async Task Shadow_mode_records_the_anomaly_but_still_writes_everything()
    {
        var inner = new CapturingInner();
        var quarantine = new CapturingQuarantine();
        var store = NewStore(inner, quarantine, new QuarantineOptions { ShadowMode = true });

        await store.AddBatchAsync([Good, Bad]);

        inner.Written.Should().HaveCount(2, "shadow mode diverts nothing");
        quarantine.Written.Should().ContainSingle(
            "shadow mode still records what it would have diverted, so it can be inspected");
    }

    [Fact]
    public async Task A_failing_quarantine_write_never_costs_the_sale()
    {
        var inner = new CapturingInner();
        var store = NewStore(inner, new ThrowingQuarantine(), new QuarantineOptions { ShadowMode = false });

        var act = async () => await store.AddBatchAsync([Good, Bad]);

        await act.Should().NotThrowAsync();
        inner.Written.Should().Contain(Good);
    }

    [Fact]
    public async Task Disabled_passes_the_batch_straight_through()
    {
        var inner = new CapturingInner();
        var quarantine = new CapturingQuarantine();
        var store = NewStore(inner, quarantine, new QuarantineOptions { Enabled = false });

        await store.AddBatchAsync([Good, Bad]);

        inner.Written.Should().HaveCount(2);
        quarantine.Written.Should().BeEmpty();
    }
}

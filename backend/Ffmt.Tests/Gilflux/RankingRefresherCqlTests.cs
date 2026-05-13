using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Gilflux;

public sealed class RankingRefresherCqlTests
{
    private static (ScyllaRankingRefresher Refresher, List<string> Captured) NewRefresher()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        var options = Options.Create(new GilfluxOptions());
        return (new ScyllaRankingRefresher(session, options, NullLogger<ScyllaRankingRefresher>.Instance), captured);
    }

    [Fact]
    public async Task RefreshAsync_PreparesSingleInsertIntoGilfluxRankings()
    {
        var (refresher, captured) = NewRefresher();
        try { await refresher.RefreshAsync(21, 12345); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("INSERT INTO gilflux_rankings"));
        captured.Should().NotContain(c => c.Contains("INSERT INTO gilflux_by_world"));
        captured.Should().Contain(c => c.Contains("SUM(total_price)"));
    }

    [Fact]
    public async Task RefreshAsync_PreparesMaxSaleTimeQuery()
    {
        var (refresher, captured) = NewRefresher();
        try { await refresher.RefreshAsync(21, 12345); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("MAX(sale_time)") && c.Contains("FROM sales"));
    }
}

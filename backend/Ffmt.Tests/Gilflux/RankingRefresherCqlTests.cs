using Cassandra;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging.Abstractions;
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
        return (new ScyllaRankingRefresher(session, NullLogger<ScyllaRankingRefresher>.Instance), captured);
    }

    [Fact]
    public async Task RefreshAsync_PreparesBothCanonicalAndCompanionInsert()
    {
        var (refresher, captured) = NewRefresher();
        try { await refresher.RefreshAsync(21, 12345); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("INSERT INTO gilflux_ranking"));
        captured.Should().Contain(c => c.Contains("INSERT INTO gilflux_by_world"));
        captured.Should().Contain(c => c.Contains("SUM(total_price)"));
        captured.Should().NotContain(c => c.Contains("ranking_alltime"));
    }

    [Fact]
    public async Task RefreshAsync_PreparesMaxSaleTimeQuery()
    {
        var (refresher, captured) = NewRefresher();
        try { await refresher.RefreshAsync(21, 12345); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("MAX(sale_time)") && c.Contains("FROM sales"));
    }
}

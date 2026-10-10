using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class SaleStoreMannequinScanCqlTests
{
    [Fact]
    public async Task GetMannequinInTokenRangeAsync_filters_one_token_slice_server_side()
    {
        var (session, captured) = CapturingScyllaSession.New();
        var store = new ScyllaSaleStore(session, NullLogger<ScyllaSaleStore>.Instance);

        try { await store.GetMannequinInTokenRangeAsync(long.MinValue, 0); } catch { }

        captured.Should().Contain(c =>
            c.Contains("FROM sales") &&
            c.Contains("token(item_id, world_id) > ?") &&
            c.Contains("token(item_id, world_id) <= ?") &&
            c.Contains("on_mannequin = true") &&
            c.Contains("ALLOW FILTERING"));
    }
}

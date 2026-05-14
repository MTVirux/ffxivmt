using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class ArchiveStoreCqlTests
{
    [Fact]
    public async Task IsExportedAsync_PreparesSelectAgainstArchiveExportState()
    {
        var (store, captured) = NewStore();

        try { await store.IsExportedAsync(21, new DateOnly(2026, 5, 1)); } catch { }

        captured.Should().ContainSingle()
            .Which.Should().Contain("FROM ffmt.archive_export_state")
            .And.Contain("WHERE world_id = ?")
            .And.Contain("AND export_date = ?");
    }

    [Fact]
    public async Task MarkExportedAsync_PreparesInsertAgainstArchiveExportState()
    {
        var (store, captured) = NewStore();

        try { await store.MarkExportedAsync(21, new DateOnly(2026, 5, 1)); } catch { }

        captured.Should().Contain(c => c.Contains("INTO ffmt.archive_export_state"));
    }

    private static (ScyllaArchiveStore Store, List<string> Captured) NewStore()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Cassandra.PreparedStatement>(null!));
        return (new ScyllaArchiveStore(session), captured);
    }
}

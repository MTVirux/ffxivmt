using Cassandra;
using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Fakes;

internal static class CapturingScyllaSession
{
    /// <summary>Records every prepared CQL string. Execution is left unstubbed, so callers run the
    /// store inside a try/catch and assert on the captured CQL rather than the driver round-trip.</summary>
    public static (IScyllaSession Session, List<string> Captured) New()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        return (session, captured);
    }
}

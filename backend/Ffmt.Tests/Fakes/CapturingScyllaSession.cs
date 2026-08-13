using Cassandra;
using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Fakes;

// Captures prepared CQL strings; execution is unstubbed, so callers try/catch the call and assert on the CQL.
internal static class CapturingScyllaSession
{
    public static (IScyllaSession Session, List<string> Captured) New()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        return (session, captured);
    }
}

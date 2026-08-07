using Cassandra;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>Null-tolerant column readers - columns written before a schema change come back null.</summary>
internal static class RowExtensions
{
    public static bool SafeBool(this Row row, string column) =>
        !row.IsNull(column) && row.GetValue<bool>(column);

    public static int SafeInt(this Row row, string column) =>
        row.IsNull(column) ? 0 : row.GetValue<int>(column);

    public static DateTimeOffset? SafeTimestamp(this Row row, string column) =>
        row.IsNull(column) ? null : row.GetValue<DateTimeOffset>(column);

    public static long? SafeEpochMs(this Row row, string column) =>
        row.IsNull(column) ? null : row.GetValue<DateTimeOffset>(column).ToUnixTimeMilliseconds();
}

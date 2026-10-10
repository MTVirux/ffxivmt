using System.Numerics;

namespace Ffmt.Core.Storage.Scylla;

public static class TokenRanges
{
    /// <summary>Contiguous (Start, End] slices of the Murmur3 ring. Murmur3 never yields long.MinValue,
    /// so the open lower bound of the first slice loses nothing.</summary>
    public static IReadOnlyList<(long Start, long End)> Split(int count)
    {
        var span = (BigInteger)long.MaxValue - long.MinValue;
        var ranges = new List<(long Start, long End)>(count);
        var start = long.MinValue;

        for (var i = 1; i <= count; i++)
        {
            var end = i == count ? long.MaxValue : (long)(long.MinValue + span * i / count);
            ranges.Add((start, end));
            start = end;
        }

        return ranges;
    }
}

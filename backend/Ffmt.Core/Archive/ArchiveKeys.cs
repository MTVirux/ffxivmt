namespace Ffmt.Core.Archive;

/// <summary>The object-storage layout is frozen: every key already in the bucket was written by
/// these formats, so changing them orphans the archive.</summary>
public static class ArchiveKeys
{
    public const string ArchivePrefix = "archive/";
    public const string CorrectionsPrefix = "corrections/";

    public static string For(string prefix, DateOnly date, string dc, string world) =>
        $"{prefix}{date.Year}/{date.Month:D2}/{date.Day:D2}/{dc}/{world}.parquet";

    public static string ToArchiveKey(string correctionsKey) =>
        ArchivePrefix + correctionsKey[CorrectionsPrefix.Length..];
}

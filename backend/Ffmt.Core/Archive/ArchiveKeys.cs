namespace Ffmt.Core.Archive;

// Key layout is frozen - every key in the bucket was written by these formats. Never build one by hand.
public static class ArchiveKeys
{
    public const string ArchivePrefix = "archive/";
    public const string CorrectionsPrefix = "corrections/";

    public static string For(string prefix, DateOnly date, string dc, string world) =>
        $"{prefix}{date.Year}/{date.Month:D2}/{date.Day:D2}/{dc}/{world}.parquet";

    public static string ToArchiveKey(string correctionsKey) =>
        ArchivePrefix + correctionsKey[CorrectionsPrefix.Length..];
}

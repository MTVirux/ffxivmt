namespace Ffmt.Core.Models;

public sealed record GilfluxRanking(
    int ItemId,
    int? WorldId,
    IReadOnlyDictionary<string, long> Rankings,
    long? UpdatedAt,
    long? LastSaleTime);

namespace Ffmt.Core.Models;

public sealed record GilfluxRanking(
    int ItemId,
    int? WorldId,
    long Ranking1h,
    long Ranking3h,
    long Ranking6h,
    long Ranking12h,
    long Ranking1d,
    long Ranking3d,
    long Ranking7d,
    long? UpdatedAt,
    long? LastSaleTime);

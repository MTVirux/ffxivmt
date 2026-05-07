namespace Ffmt.Core.Models;

public sealed record GilfluxRanking(
    int ItemId,
    string ItemName,
    int? WorldId,
    string? WorldName,
    string Datacenter,
    string Region,
    long RankingAlltime,
    long Ranking1h,
    long Ranking3h,
    long Ranking6h,
    long Ranking12h,
    long Ranking1d,
    long Ranking3d,
    long Ranking7d,
    long? UpdatedAt,
    long? LastSaleTime);

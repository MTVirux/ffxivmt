using System.Text.Json.Serialization;

namespace Ffmt.Core.Models;

public sealed record GilfluxRanking(
    int ItemId,
    int? WorldId,
    [property: JsonPropertyName("ranking_1h")]  long Ranking1h,
    [property: JsonPropertyName("ranking_3h")]  long Ranking3h,
    [property: JsonPropertyName("ranking_6h")]  long Ranking6h,
    [property: JsonPropertyName("ranking_12h")] long Ranking12h,
    [property: JsonPropertyName("ranking_1d")]  long Ranking1d,
    [property: JsonPropertyName("ranking_3d")]  long Ranking3d,
    [property: JsonPropertyName("ranking_7d")]  long Ranking7d,
    long? UpdatedAt,
    long? LastSaleTime);

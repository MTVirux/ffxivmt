namespace Ffmt.Core.Models;

public sealed record Item(
    int Id,
    string Name,
    bool Marketable,
    bool Craftable,
    int IconImage);

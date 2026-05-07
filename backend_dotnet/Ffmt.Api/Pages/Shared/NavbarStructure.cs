namespace Ffmt.Api.Pages.Shared;

/// <summary>
/// Mirrors <c>backend/application/config/navbar.php</c>. Single source of truth for the navbar
/// items rendered by <see cref="_Navbar"/> and the cards on the home page.
/// </summary>
public static class NavbarStructure
{
    public sealed record Entry(string Name, string Link, string Description);

    public static readonly IReadOnlyList<Entry> Items =
    [
        new(
            Name: "Gilflux",
            Link: "/gilflux",
            Description: "Show's the top items that moved the most gil for the selected World, DC or Region."),
        new(
            Name: "Currency Efficiency Calculator",
            Link: "/tools/currency-efficiency-calculator",
            Description: "The most profitable way to spend your currency."),
        new(
            Name: "Item Product Profit Solver",
            Link: "/tools/item-product-profit-calculator",
            Description: "The most profitable craft for a certain material."),
    ];

    public const string GithubLink = "https://github.com/MTVirux/ffxivmt";
}

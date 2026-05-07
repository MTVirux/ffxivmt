using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ffmt.Api.Pages;

/// <summary>
/// <c>GET /item/{id:int}</c>. The legacy PHP <c>Item</c> controller loaded a non-existent
/// <c>'item'</c> view (only <c>item_info.php</c> shipped) and the view itself referenced
/// <c>$item-&gt;prices-&gt;listings</c> which the controller never set — the page was dead.
/// This Razor port renders a clean info card from the Scylla items table; the actual MB
/// listings rendering can grow off this page when the front-end needs it.
/// </summary>
public sealed class ItemModel(IItemStore items) : PageModel
{
    public Ffmt.Core.Models.Item? Item { get; private set; }

    public async Task<IActionResult> OnGetAsync([FromRoute] int id, CancellationToken ct)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        Item = await items.GetAsync(id, ct);
        if (Item is null)
        {
            return NotFound();
        }
        return Page();
    }
}

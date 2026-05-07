using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Ffmt.Api.Pages;

public sealed class ErrorModel : PageModel
{
    public int Status { get; private set; } = 500;
    public string Message { get; private set; } = "Something went wrong.";

    public void OnGet([FromRoute] int? statusCode)
    {
        Status = statusCode ?? 500;
        Message = Status switch
        {
            404 => "The page you requested does not exist.",
            400 => "The request was invalid.",
            503 => "The site is temporarily unavailable.",
            _   => "Something went wrong.",
        };
    }
}

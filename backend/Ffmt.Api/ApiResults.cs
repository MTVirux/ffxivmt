using Microsoft.AspNetCore.Http;

namespace Ffmt.Api;

internal static class ApiResults
{
    public static IResult Fail(string message, int statusCode) =>
        Results.Json(new { status = false, message }, statusCode: statusCode);

    public static IResult Ok(string message, object data) =>
        Results.Ok(new { status = true, message, data });
}

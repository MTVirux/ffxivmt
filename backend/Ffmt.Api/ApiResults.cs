using Microsoft.AspNetCore.Http;

namespace Ffmt.Api;

/// <summary>The two response envelopes every endpoint shares. Key casing comes from the
/// global serializer options in <c>Program.cs</c>.</summary>
internal static class ApiResults
{
    public static IResult Fail(string message, int statusCode) =>
        Results.Json(new { status = false, message }, statusCode: statusCode);

    public static IResult Ok(string message, object data) =>
        Results.Ok(new { status = true, message, data });
}

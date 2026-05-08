using Cassandra;
using Ffmt.Core.Storage.Elastic;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ffmt.Api;

public sealed class BackendUnavailableExceptionHandler(ILogger<BackendUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (NoHostAvailableException or DriverException or ElasticsearchUnavailableException))
        {
            return false;
        }

        logger.LogWarning(exception, "Backend unavailable on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(
            new { status = false, message = "Backend unavailable" },
            cancellationToken);
        return true;
    }
}

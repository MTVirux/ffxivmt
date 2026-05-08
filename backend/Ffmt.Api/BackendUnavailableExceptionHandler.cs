using Cassandra;
using Ffmt.Core.Storage.Elastic;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ffmt.Api;

/// <summary>
/// Maps storage-availability exceptions (Scylla unreachable, Elasticsearch transport errors) to
/// a 503 with a small JSON envelope matching the legacy PHP error shape.
/// </summary>
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

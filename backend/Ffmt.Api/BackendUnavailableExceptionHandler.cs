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
        if (exception is not (DriverException or ElasticsearchUnavailableException))
        {
            return false;
        }

        logger.LogWarning(exception, "Backend unavailable on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        await ApiResults
            .Fail("Backend unavailable", StatusCodes.Status503ServiceUnavailable)
            .ExecuteAsync(httpContext);
        return true;
    }
}

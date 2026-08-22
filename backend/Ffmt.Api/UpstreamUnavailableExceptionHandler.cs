using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ffmt.Api;

/// <summary>A Universalis or Garland outage is their failure, not ours - report it as a 502 the
/// caller can act on rather than an opaque 500.</summary>
public sealed class UpstreamUnavailableExceptionHandler(ILogger<UpstreamUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (HttpRequestException or TaskCanceledException or TimeoutException))
        {
            return false;
        }

        // A caller that hung up mid-request is not an upstream failure.
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        logger.LogWarning(exception, "Upstream API failed on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        await ApiResults
            .Fail("Upstream market data is unavailable, please try again later", StatusCodes.Status502BadGateway)
            .ExecuteAsync(httpContext);
        return true;
    }
}

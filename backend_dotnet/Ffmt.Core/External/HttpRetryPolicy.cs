using System.Net;
using Polly;

namespace Ffmt.Core.External;

internal static class HttpRetryPolicy
{
    /// <summary>
    /// Builds a Polly retry policy for transient HTTP failures: connection errors,
    /// timeouts, 408, 429, and any 5xx. Backoff is exponential starting at
    /// <paramref name="initialBackoffSeconds"/>, capped at <paramref name="maxBackoffSeconds"/>.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> Build(int maxRetries, double initialBackoffSeconds, double maxBackoffSeconds)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(IsTransientStatus)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(maxRetries, attempt =>
            {
                var delay = initialBackoffSeconds * Math.Pow(2, attempt - 1);
                return TimeSpan.FromSeconds(Math.Min(delay, maxBackoffSeconds));
            });
    }

    private static bool IsTransientStatus(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        return code >= 500
            || response.StatusCode == HttpStatusCode.RequestTimeout
            || response.StatusCode == HttpStatusCode.TooManyRequests;
    }
}

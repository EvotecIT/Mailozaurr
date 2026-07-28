using System.Net;

namespace Mailozaurr;

/// <summary>
/// Owns HTTP failure classification and retry timing for provider transports.
/// </summary>
internal static class HttpRetryPolicy {
    /// <summary>
    /// Creates an HTTP exception that preserves the provider response status.
    /// </summary>
    internal static HttpRequestException CreateFailure(HttpStatusCode statusCode, string message) {
#if NET5_0_OR_GREATER
        return new HttpRequestException(message, null, statusCode);
#else
        return new ProviderHttpRequestException(message, statusCode);
#endif
    }

    /// <summary>
    /// Determines whether another attempt is allowed.
    /// </summary>
    internal static bool ShouldRetry(Exception exception, int attempt, int retryCount, bool retryAlways) =>
        attempt < Math.Max(0, retryCount) &&
        (retryAlways || Helpers.IsTransient(exception));

    /// <summary>
    /// Delays before the next attempt using bounded exponential backoff and jitter.
    /// </summary>
    internal static Task DelayAsync(
        int baseDelayMilliseconds,
        double backoff,
        int attempt,
        int maxDelayMilliseconds,
        int jitterMilliseconds,
        CancellationToken cancellationToken) {
        if (baseDelayMilliseconds <= 0) {
            return Task.CompletedTask;
        }

        var safeBackoff = double.IsNaN(backoff) || backoff < 0 ? 0 : backoff;
        var computed = baseDelayMilliseconds * Math.Pow(safeBackoff, Math.Max(0, attempt));
        var cap = maxDelayMilliseconds > 0 ? maxDelayMilliseconds : int.MaxValue - 1;
        var bounded = double.IsInfinity(computed) || computed > cap
            ? cap
            : Math.Max(0, computed);
        var delay = (long)Math.Round(bounded);

        if (jitterMilliseconds > 0 && delay > 0) {
            var jitterBound = Math.Min(jitterMilliseconds, int.MaxValue - 1);
            delay = Math.Min(cap, delay + GraphRetryHelperRandom.NextInt(jitterBound + 1));
        }

        return delay > 0
            ? Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken)
            : Task.CompletedTask;
    }

#if !NET5_0_OR_GREATER
    internal sealed class ProviderHttpRequestException : HttpRequestException {
        internal ProviderHttpRequestException(string message, HttpStatusCode statusCode)
            : base(message) {
            StatusCode = statusCode;
        }

        internal HttpStatusCode StatusCode { get; }
    }
#endif
}

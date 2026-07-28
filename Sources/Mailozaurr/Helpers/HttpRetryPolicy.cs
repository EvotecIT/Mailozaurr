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
    internal static bool ShouldRetry(
        Exception exception,
        int attempt,
        int retryCount,
        bool retryAlways,
        bool isKnownTransient = false) =>
        attempt < Math.Max(0, retryCount) &&
        (retryAlways || isKnownTransient || Helpers.IsTransient(exception));

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
        var delay = RetryDelayCalculator.Calculate(
            baseDelayMilliseconds,
            backoff,
            attempt,
            maxDelayMilliseconds,
            jitterMilliseconds);
        return delay > TimeSpan.Zero
            ? Task.Delay(delay, cancellationToken)
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

using System.Net;

namespace Mailozaurr;

/// <summary>
/// Classification and backoff helpers for Microsoft Graph send operations.
/// </summary>
internal static class GraphRetryHelper {
    private const int StatusRequestTimeout = 408;
    private const int StatusTooManyRequests = 429;
    private const int StatusServerErrorMin = 500;
    private const int StatusServerErrorMax = 599;
    private static readonly string[] ThrottleMarkers = new[]
    {
        "ApplicationThrottled",
        "MailboxConcurrency",
        "TooManyRequests",
        "Throttled"
    };

    internal static bool IsThrottled(Exception ex) {
        if (ex is GraphApiException gex) {
            if ((int)gex.StatusCode == StatusTooManyRequests) {
                return true;
            }
            var parsed = GraphApiErrorParser.Parse(gex.ResponseContent, gex.StatusCode);
            var code = parsed?.Error?.Code ?? string.Empty;
            foreach (var marker in ThrottleMarkers) {
                if (code.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            if (!string.IsNullOrWhiteSpace(parsed?.Raw)) {
                foreach (var marker in ThrottleMarkers) {
                    if (parsed!.Raw.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
        }

        if (ex is HttpRequestException httpEx) {
#if NET5_0_OR_GREATER
            if (httpEx.StatusCode.HasValue && (int)httpEx.StatusCode.Value == StatusTooManyRequests)
                return true;
#endif
        }

        return false;
    }

    internal static bool IsTransient(Exception ex) {
        if (IsThrottled(ex)) {
            return true;
        }

        if (ex is GraphApiException gex) {
            var code = (int)gex.StatusCode;
            if (code == StatusRequestTimeout || code == StatusTooManyRequests)
                return true;
            if (code >= StatusServerErrorMin && code <= StatusServerErrorMax)
                return true;

            var parsed = GraphApiErrorParser.Parse(gex.ResponseContent, gex.StatusCode);
            var message = parsed?.Error?.Message ?? parsed?.Raw ?? string.Empty;
            if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (message.IndexOf("temporarily", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (message.IndexOf("gateway", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (message.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        // Fall back to generic library-wide transient classification.
        return Helpers.IsTransient(ex);
    }

    /// <summary>
    /// Calculate exponential backoff delay with optional jitter and cap.
    /// </summary>
    internal static TimeSpan CalculateDelay(GraphSendPolicy policy, int attempt) =>
        RetryDelayCalculator.Calculate(
            policy.BaseDelayMs,
            2.0,
            attempt,
            policy.MaxDelayMs,
            policy.JitterMs);
}

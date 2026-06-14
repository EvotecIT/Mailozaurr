using System.Net;
using System.Threading;

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
            var parsed = GraphApiErrorParser.Parse(gex.ResponseContent);
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

            var parsed = GraphApiErrorParser.Parse(gex.ResponseContent);
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
    internal static TimeSpan CalculateDelay(GraphSendPolicy policy, int attempt) {
        if (attempt < 0) attempt = 0;
        var baseDelay = (double)Math.Max(0, policy.BaseDelayMs);
        var delay = baseDelay * Math.Pow(2, attempt);
        var max = Math.Max(0, policy.MaxDelayMs);
        if (max > 0) {
            delay = Math.Min(delay, max);
        }

        var jitterWindow = Math.Max(0, policy.JitterMs);
        if (jitterWindow > 0) {
            var jitter = GraphRetryHelperRandom.NextInt(jitterWindow + 1);
            delay += jitter;
        }

        return TimeSpan.FromMilliseconds(Math.Max(0, (int)Math.Round(delay)));
    }
}

internal static class GraphRetryHelperRandom {
#if !NET5_0_OR_GREATER
    [ThreadStatic]
    private static Random? s_random;
#endif

    internal static int NextInt(int maxExclusive) {
        if (maxExclusive <= 1) return 0;
#if NET5_0_OR_GREATER
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, maxExclusive);
#else
        var rnd = s_random ??= new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId));
        return rnd.Next(0, maxExclusive);
#endif
    }
}
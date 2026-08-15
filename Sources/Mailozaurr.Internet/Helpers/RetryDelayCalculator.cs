namespace Mailozaurr;

/// <summary>
/// Calculates bounded exponential retry delays consistently across transports.
/// </summary>
internal static class RetryDelayCalculator {
#if !NET5_0_OR_GREATER
    [ThreadStatic]
    private static Random? s_random;
#endif

    internal static TimeSpan Calculate(
        int baseDelayMilliseconds,
        double backoff,
        int attempt,
        int maxDelayMilliseconds,
        int jitterMilliseconds) {
        if (baseDelayMilliseconds <= 0) {
            return TimeSpan.Zero;
        }

        var safeBackoff = double.IsNaN(backoff) || backoff < 0
            ? 0
            : backoff;
        var cap = maxDelayMilliseconds > 0
            ? maxDelayMilliseconds
            : int.MaxValue - 1;
        var computed = baseDelayMilliseconds * Math.Pow(safeBackoff, Math.Max(0, attempt));
        var bounded = double.IsInfinity(computed) || computed > cap
            ? cap
            : Math.Max(0, computed);
        var delay = (long)Math.Round(bounded);

        if (jitterMilliseconds > 0 && delay > 0) {
            var jitterBound = Math.Min(jitterMilliseconds, int.MaxValue - 1);
            delay = Math.Min(
                cap,
                delay + NextInt(jitterBound + 1));
        }

        return delay > 0
            ? TimeSpan.FromMilliseconds(delay)
            : TimeSpan.Zero;
    }

    private static int NextInt(int maxExclusive) {
        if (maxExclusive <= 1) {
            return 0;
        }
#if NET5_0_OR_GREATER
        return System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, maxExclusive);
#else
        var random = s_random ??= new Random(
            unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId));
        return random.Next(0, maxExclusive);
#endif
    }
}

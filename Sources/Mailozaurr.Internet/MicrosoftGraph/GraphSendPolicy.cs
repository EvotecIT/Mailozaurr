namespace Mailozaurr;

/// <summary>
/// Policy controlling how Graph send operations are throttled and retried.
/// </summary>
public sealed class GraphSendPolicy {
    /// <summary>Maximum number of concurrent Graph HTTP requests. Applies globally.</summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Maximum number of retries upon transient errors.</summary>
    public int MaxRetries { get; set; } = 4;

    /// <summary>Base delay in milliseconds for backoff calculation.</summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>Maximum delay in milliseconds between retries.</summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>Jitter window in milliseconds added to each delay.</summary>
    public int JitterMs { get; set; } = 500;

    /// <summary>Retry only when the error is classified as transient or throttling related.</summary>
    public bool RetryOnTransient { get; set; } = true;

    /// <summary>When true and SMTP is configured, fallback to SMTP after Graph retries are exhausted.</summary>
    public bool EnableSmtpFallback { get; set; } = false;

    /// <summary>Returns a conservative default policy suitable for most workloads.</summary>
    public static GraphSendPolicy Default => new GraphSendPolicy();
}
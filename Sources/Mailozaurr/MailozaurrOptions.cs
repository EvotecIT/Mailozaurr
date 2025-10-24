namespace Mailozaurr;

/// <summary>
/// Global options for Mailozaurr behavior.
/// </summary>
public static class MailozaurrOptions
{
    static MailozaurrOptions()
    {
        // Internal default for PowerShell and other hosts that rebuild or do not execute psm1 init.
        // Can be overridden at runtime by assigning DefaultGraphPolicy.
        DefaultGraphPolicy = GraphSendPolicy.Default;
        if (DefaultGraphPolicy.MaxConcurrency > 0)
        {
            MicrosoftGraphUtils.MaxConcurrentRequests = DefaultGraphPolicy.MaxConcurrency;
        }
    }
    /// <summary>
    /// Default policy applied to all Graph send operations when an instance policy is not supplied.
    /// Leave <c>null</c> to preserve legacy behavior (no policy applied by default).
    /// </summary>
    public static GraphSendPolicy? DefaultGraphPolicy { get; set; }

    /// <summary>
    /// Optional global factory used to construct an SMTP sender for fallback scenarios.
    /// If set and the active <see cref="GraphSendPolicy"/> enables fallback, the factory will be invoked.
    /// </summary>
    public static Func<Graph, Smtp?>? SmtpFallbackFactory { get; set; }
}

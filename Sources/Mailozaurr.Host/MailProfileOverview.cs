namespace Mailozaurr.Hosting;

/// <summary>
/// Aggregated summary of a saved Mailozaurr profile for operator and agent surfaces.
/// </summary>
public sealed class MailProfileOverview {
    /// <summary>The saved profile.</summary>
    public MailProfile Profile { get; set; } = new();

    /// <summary>Normalized capability map for the profile.</summary>
    public ProfileCapabilities? Capabilities { get; set; }

    /// <summary>Whether the profile supports mailbox read/search operations.</summary>
    public bool SupportsRead { get; set; }

    /// <summary>Whether the profile supports outbound sending.</summary>
    public bool SupportsSend { get; set; }

    /// <summary>Persisted authentication status when available.</summary>
    public MailProfileAuthStatus? AuthStatus { get; set; }

    /// <summary>Provider-readiness diagnosis.</summary>
    public MailProfileValidationResult? Readiness { get; set; }

    /// <summary>Whether the saved profile currently appears ready to use.</summary>
    public bool IsReady { get; set; }

    /// <summary>Total validation or readiness errors surfaced for the profile.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Total validation or readiness warnings surfaced for the profile.</summary>
    public int WarningCount { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string Summary { get; set; } = string.Empty;
}
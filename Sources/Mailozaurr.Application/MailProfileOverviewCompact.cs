namespace Mailozaurr.Application;

/// <summary>
/// Lightweight projection of a profile overview for list and agent scenarios.
/// </summary>
public sealed class MailProfileOverviewCompact {
    /// <summary>The stable profile identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The human-readable profile name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The profile provider kind.</summary>
    public MailProfileKind Kind { get; set; }

    /// <summary>Whether the profile is marked as default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Whether the profile supports mailbox reads.</summary>
    public bool SupportsRead { get; set; }

    /// <summary>Whether the profile supports sending.</summary>
    public bool SupportsSend { get; set; }

    /// <summary>The persisted auth mode when known.</summary>
    public string? AuthMode { get; set; }

    /// <summary>Whether the profile currently appears ready to use.</summary>
    public bool IsReady { get; set; }

    /// <summary>Total readiness or validation errors.</summary>
    public int ErrorCount { get; set; }

    /// <summary>Total readiness or validation warnings.</summary>
    public int WarningCount { get; set; }

    /// <summary>Short human-readable summary line.</summary>
    public string Summary { get; set; } = string.Empty;
}

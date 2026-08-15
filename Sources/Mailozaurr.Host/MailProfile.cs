namespace Mailozaurr.Hosting;

/// <summary>
/// Represents a reusable profile definition that can be shared by CLI, MCP, GUI, and PowerShell adapters.
/// </summary>
public sealed class MailProfile {
    /// <summary>Stable profile identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing profile name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional description for human operators.</summary>
    public string? Description { get; set; }

    /// <summary>Technology or provider behind the profile.</summary>
    public MailProfileKind Kind { get; set; } = MailProfileKind.Unknown;

    /// <summary>Default sender address when the profile supports sending.</summary>
    public string? DefaultSender { get; set; }

    /// <summary>Default mailbox address or principal name, where applicable.</summary>
    public string? DefaultMailbox { get; set; }

    /// <summary>Whether this profile should be treated as the default choice.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Non-secret profile settings.</summary>
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Explicit capability override when present.
    /// </summary>
    public ProfileCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Returns the effective capabilities for this profile.
    /// </summary>
    public ProfileCapabilities GetCapabilities() => Capabilities ?? ProfileCapabilities.CreateDefault(Kind);

    /// <summary>
    /// Returns the effective capabilities after applying the handlers available in the current application.
    /// Explicit profile overrides remain restrictive; otherwise the registered handlers define the capability set.
    /// </summary>
    internal ProfileCapabilities GetCapabilities(MailCapability availableCapabilities) {
        MailCapability configured = Capabilities?.Capabilities ?? availableCapabilities;
        return new ProfileCapabilities(Kind, configured & availableCapabilities);
    }
}

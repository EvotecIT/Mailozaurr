namespace Mailozaurr.Application;

/// <summary>
/// Describes the capabilities available for a given mail profile kind.
/// </summary>
public sealed class ProfileCapabilities {
    /// <summary>
    /// Creates a new capability description.
    /// </summary>
    /// <param name="kind">Profile kind the capabilities apply to.</param>
    /// <param name="capabilities">Supported operations.</param>
    public ProfileCapabilities(MailProfileKind kind, MailCapability capabilities) {
        Kind = kind;
        Capabilities = capabilities;
    }

    /// <summary>Profile kind the capabilities apply to.</summary>
    public MailProfileKind Kind { get; }

    /// <summary>Flags describing supported operations.</summary>
    public MailCapability Capabilities { get; }

    /// <summary>
    /// Returns <c>true</c> when all requested capabilities are supported.
    /// </summary>
    public bool Supports(MailCapability capability) => (Capabilities & capability) == capability;

    /// <summary>
    /// Creates a new instance using the default capability map for <paramref name="kind" />.
    /// </summary>
    public static ProfileCapabilities CreateDefault(MailProfileKind kind) => MailCapabilityCatalog.For(kind);
}
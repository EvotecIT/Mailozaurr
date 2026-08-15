namespace Mailozaurr;

/// <summary>
/// Identifies a mailbox or account container.
/// </summary>
public sealed class MailboxRef {
    /// <summary>Owning profile identifier.</summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>Provider-specific mailbox identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-facing mailbox name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Mailbox address or principal name when known.</summary>
    public string? Address { get; set; }
}
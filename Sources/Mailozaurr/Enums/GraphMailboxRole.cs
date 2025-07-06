namespace Mailozaurr;

/// <summary>
/// Specifies roles available for mailbox permissions.
/// </summary>
public enum GraphMailboxRole {
    /// <summary>Full owner access.</summary>
    Owner,

    /// <summary>Read-only access.</summary>
    Read,

    /// <summary>Write access.</summary>
    Write,

    /// <summary>Other or custom role not represented by predefined values.</summary>
    Custom
}

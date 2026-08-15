namespace Mailozaurr.Hosting;

/// <summary>
/// Describes the depth of a live profile connection test.
/// </summary>
public enum MailProfileConnectionTestScope {
    /// <summary>Uses the provider-appropriate default probe.</summary>
    Auto,

    /// <summary>Verifies session creation and authentication only.</summary>
    Auth,

    /// <summary>Verifies a lightweight mailbox read operation.</summary>
    Mailbox,

    /// <summary>Verifies a non-destructive send-path preflight.</summary>
    Send
}
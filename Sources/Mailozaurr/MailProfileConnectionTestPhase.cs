namespace Mailozaurr;

/// <summary>
/// Identifies an observable phase of a profile connection test.
/// </summary>
public enum MailProfileConnectionTestPhase {
    /// <summary>Resolving and validating the saved profile.</summary>
    Profile,

    /// <summary>Creating the provider session and resolving its authentication material.</summary>
    Session,

    /// <summary>Running a lightweight provider or authenticated-session probe.</summary>
    Probe,

    /// <summary>Running a lightweight mailbox read probe.</summary>
    Mailbox,

    /// <summary>Running a non-destructive send-path preflight.</summary>
    SendPreflight
}

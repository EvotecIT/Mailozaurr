namespace Mailozaurr;

/// <summary>
/// Records one observable phase of a profile connection test.
/// </summary>
public sealed class MailProfileConnectionTestStage : OperationResult {
    /// <summary>Phase represented by this stage.</summary>
    public MailProfileConnectionTestPhase Phase { get; set; }

    /// <summary>Provider operation used for the phase.</summary>
    public string? Probe { get; set; }

    /// <summary>Mailbox, account, or transport target when available.</summary>
    public string? Target { get; set; }

    /// <summary>Elapsed phase duration in milliseconds.</summary>
    public long DurationMilliseconds { get; set; }

    /// <summary>Typed, non-secret evidence observed during this phase.</summary>
    public MailProfileDiagnosticEvidence? Evidence { get; set; }
}

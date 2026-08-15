namespace Mailozaurr;

/// <summary>
/// Describes duplicate sent-copy probe result for idempotent SMTP send flows.
/// </summary>
public sealed class SmtpDuplicateProbeResult {
    /// <summary>
    /// Gets a value indicating whether a matching sent copy was found.
    /// </summary>
    public bool IsMatch { get; set; }

    /// <summary>
    /// Gets a folder full name where the match was detected.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets a detected or fallback message-id associated with the match.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Gets empty match result.
    /// </summary>
    public static SmtpDuplicateProbeResult None { get; } = new();
}
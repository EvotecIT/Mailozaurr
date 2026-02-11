namespace Mailozaurr;

/// <summary>
/// Describes normalized SMTP send execution outcome for pipeline consumers.
/// </summary>
public sealed class SmtpSendExecutionResult {
    /// <summary>
    /// Gets a value indicating whether send execution completed successfully.
    /// </summary>
    public bool Ok { get; set; }

    /// <summary>
    /// Gets a value indicating whether the message was actually sent.
    /// </summary>
    public bool Sent { get; set; }

    /// <summary>
    /// Gets emitted message-id when available.
    /// </summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Gets send error when send execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets a value indicating whether append-to-sent completed.
    /// </summary>
    public bool AppendedToSent { get; set; }

    /// <summary>
    /// Gets appended sent folder name when append completed.
    /// </summary>
    public string? AppendedSentFolder { get; set; }

    /// <summary>
    /// Gets append-to-sent error when append failed.
    /// </summary>
    public string? AppendError { get; set; }
}

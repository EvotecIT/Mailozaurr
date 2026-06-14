namespace Mailozaurr;

/// <summary>
/// Describes optional append-to-sent outcome for SMTP pipeline orchestration.
/// </summary>
public sealed class SmtpAppendExecutionResult {
    /// <summary>
    /// Gets a value indicating whether append operation succeeded.
    /// </summary>
    public bool Appended { get; set; }

    /// <summary>
    /// Gets appended folder name when append succeeded.
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Gets append error when append failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets empty append outcome.
    /// </summary>
    public static SmtpAppendExecutionResult None { get; } = new();
}
namespace Mailozaurr;

/// <summary>
/// Describes optional append-to-sent outcome for SMTP pipeline orchestration.
/// </summary>
public sealed class SmtpAppendExecutionResult {
    /// <summary>
    /// Gets a value indicating whether append operation succeeded.
    /// </summary>
    public bool Appended { get; init; }

    /// <summary>
    /// Gets appended folder name when append succeeded.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    /// Gets append error when append failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets empty append outcome.
    /// </summary>
    public static SmtpAppendExecutionResult None { get; } = new();
}

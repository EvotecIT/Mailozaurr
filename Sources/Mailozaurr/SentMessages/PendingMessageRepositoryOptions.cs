namespace Mailozaurr;

/// <summary>
/// Options for configuring <see cref="FilePendingMessageRepository"/>.
/// </summary>
public sealed class PendingMessageRepositoryOptions {
    /// <summary>Directory where pending message files are stored.</summary>
    public string? DirectoryPath { get; set; }

    /// <summary>
    /// Function that returns the file name used within <see cref="DirectoryPath"/>.
    /// The current timestamp is provided to the delegate.
    /// </summary>
    public Func<DateTimeOffset, string>? FileNameFactory { get; set; }
}

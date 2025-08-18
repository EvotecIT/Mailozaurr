namespace Mailozaurr;

/// <summary>
/// Simplified representation of an email message returned from Graph.
/// </summary>
/// <remarks>
/// Only a subset of fields are exposed to keep the object light‑weight
/// when fetching lists of messages.
/// </remarks>
public class EmailGraphMessage {
    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subject line of the message.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preview text of the message body.
    /// </summary>
    public string BodyPreview { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full body content of the message.
    /// </summary>
    public object? Body { get; set; }

    /// <summary>
    /// Gets or sets the change key used for concurrency checks.
    /// </summary>
    public string ChangeKey { get; set; } = string.Empty;
    // Add more properties as needed
}

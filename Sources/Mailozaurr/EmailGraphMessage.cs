namespace Mailozaurr;

/// <summary>
/// Simplified representation of an email message returned from Graph.
/// </summary>
public class EmailGraphMessage {
    /// <summary>
    /// Gets or sets the unique identifier of the message.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the subject line of the message.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the preview text of the message body.
    /// </summary>
    public string BodyPreview { get; set; }

    /// <summary>
    /// Gets or sets the full body content of the message.
    /// </summary>
    public object Body { get; set; }

    /// <summary>
    /// Gets or sets the change key used for concurrency checks.
    /// </summary>
    public string ChangeKey { get; set; }
    // Add more properties as needed
}

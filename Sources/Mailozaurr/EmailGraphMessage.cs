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
    /// Gets or sets the message subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets a short preview of the message body.
    /// </summary>
    public string BodyPreview { get; set; }

    /// <summary>
    /// Gets or sets the message body content.
    /// </summary>
    public object Body { get; set; }

    /// <summary>
    /// Gets or sets the change key for the message.
    /// </summary>
    public string ChangeKey { get; set; }
    // Add more properties as needed
}

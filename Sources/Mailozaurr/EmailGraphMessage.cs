namespace Mailozaurr;

/// <summary>
/// Simplified representation of an email message returned from Graph.
/// </summary>
public class EmailGraphMessage {
    public string Id { get; set; }
    public string Subject { get; set; }
    public string BodyPreview { get; set; }
    public object Body { get; set; }
    public string ChangeKey { get; set; }
    // Add more properties as needed
}

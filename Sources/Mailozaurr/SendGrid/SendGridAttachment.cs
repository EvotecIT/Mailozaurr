namespace Mailozaurr;

/// <summary>
/// Represents an attachment in a SendGrid message.
/// </summary>
public class SendGridAttachment {
    /// <summary>Gets or sets the filename of the attachment.</summary>
    public string Filename { get; set; }

    /// <summary>Gets or sets the content of the attachment, encoded in Base64.</summary>
    public string Content { get; set; }

    /// <summary>Gets or sets the MIME type of the attachment.</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the disposition of the attachment.</summary>
    public string Disposition { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SendGridAttachment"/> class from a file path.
    /// </summary>
    /// <param name="filePath">The path of the file to attach.</param>
    public SendGridAttachment(string filePath) {
        Filename = Path.GetFileName(filePath);
        var bytes = File.ReadAllBytes(filePath);
        Content = Convert.ToBase64String(bytes);
        Type = "application/octet-stream";
        Disposition = "attachment";
    }
}

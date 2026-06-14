using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Represents an attachment in a SendGrid message.
/// </summary>
/// <remarks>
/// The file content is stored as a Base64 string as required by the
/// SendGrid API.
/// </remarks>
public class SendGridAttachment {
    /// <summary>Gets or sets the filename of the attachment.</summary>
    public string Filename { get; set; }

    /// <summary>Gets or sets the content of the attachment, encoded in Base64.</summary>
    public string Content { get; set; }

    /// <summary>Gets or sets the MIME type of the attachment.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Gets or sets the disposition of the attachment.</summary>
    public string Disposition { get; set; } = "attachment";

    /// <summary>Gets or sets the optional content identifier of the attachment.</summary>
    public string? ContentId { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SendGridAttachment"/> class using raw content.
    /// </summary>
    /// <param name="fileName">File name to associate with the attachment.</param>
    /// <param name="content">Attachment content as a byte array.</param>
    /// <param name="contentType">Optional MIME type of the attachment.</param>
    /// <param name="disposition">Disposition applied to the attachment.</param>
    /// <param name="contentId">Optional content identifier for inline attachments.</param>
    public SendGridAttachment(string fileName, byte[] content, string? contentType = null, string? disposition = "attachment", string? contentId = null) {
        if (string.IsNullOrWhiteSpace(fileName)) {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        content ??= Array.Empty<byte>();

        Filename = fileName;
        Content = Convert.ToBase64String(content);
        Type = !string.IsNullOrWhiteSpace(contentType) ? contentType! : (MimeTypes.GetMimeType(fileName) ?? "application/octet-stream");
        Disposition = string.IsNullOrWhiteSpace(disposition) ? "attachment" : disposition!;
        ContentId = contentId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SendGridAttachment"/> class from a file path.
    /// </summary>
    /// <param name="filePath">The path of the file to attach.</param>
    public SendGridAttachment(string filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) {
            throw new ArgumentException("File path must be provided.", nameof(filePath));
        }

        var bytes = File.ReadAllBytes(filePath);
        var fileName = Path.GetFileName(filePath);

        Filename = fileName;
        Content = Convert.ToBase64String(bytes);
        Type = MimeTypes.GetMimeType(filePath);
        Disposition = "attachment";
    }
}
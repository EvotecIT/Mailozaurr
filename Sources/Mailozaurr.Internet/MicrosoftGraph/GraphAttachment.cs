namespace Mailozaurr;

/// <summary>
/// Represents a simple file attachment used when sending messages.
/// </summary>
/// <remarks>
/// This is the lightweight counterpart to <c>AttachmentItem</c>
/// used during message creation.
/// </remarks>
public class GraphAttachment {
    /// <summary>
    /// Gets or sets the Graph type of the attachment.
    /// </summary>
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    /// <summary>
    /// Gets or sets the attachment file name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets attachment content type.
    /// </summary>
    [JsonPropertyName("contentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file content encoded as a Base64 string.
    /// </summary>
    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this attachment should be rendered inline in the
    /// message body.
    /// </summary>
    [JsonPropertyName("isInline")]
    public bool IsInline { get; set; }

    /// <summary>
    /// Optional identifier used to reference the attachment via a <c>cid:</c>
    /// URL within the HTML body.
    /// </summary>
    [JsonPropertyName("contentId")]
    public string? ContentId { get; set; }

    /// <summary>
    /// Creates a <see cref="GraphAttachment"/> from a local file path.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>The created attachment.</returns>
    public static GraphAttachment FromFile(string filePath) {
        var fileInfo = new FileInfo(filePath);
        var descriptor = new Definitions.FileAttachmentDescriptor(filePath);
        var fileBytes = descriptor.GetContentBytes(
            Definitions.AttachmentStreamStagingOptions.DefaultMaxBytes,
            fileInfo.Length);
        var fileContentBase64 = Convert.ToBase64String(fileBytes);

        return new GraphAttachment {
            Name = fileInfo.Name,
            ContentBytes = fileContentBase64
        };
    }

    /// <summary>
    /// Creates a Graph attachment from a transport-neutral attachment descriptor.
    /// </summary>
    /// <param name="descriptor">Attachment content and MIME metadata.</param>
    /// <param name="inline">Optional override indicating whether the attachment is inline.</param>
    /// <returns>The created Graph attachment.</returns>
    public static GraphAttachment FromDescriptor(Definitions.AttachmentDescriptor descriptor, bool? inline = null) {
        return FromDescriptor(descriptor, descriptor?.Length, inline);
    }

    internal static GraphAttachment FromDescriptor(
        Definitions.AttachmentDescriptor descriptor,
        long? expectedLength,
        bool? inline = null) {
        if (descriptor == null) {
            throw new ArgumentNullException(nameof(descriptor));
        }

        var fileName = descriptor.FileName;
        if (string.IsNullOrWhiteSpace(fileName) && descriptor.SourcePath is string sourcePath) {
            fileName = Path.GetFileName(sourcePath);
        }
        fileName ??= "attachment";

        var isInline = inline ?? string.Equals(
            descriptor.ContentDisposition?.Disposition,
            MimeKit.ContentDisposition.Inline,
            StringComparison.OrdinalIgnoreCase);

        return new GraphAttachment {
            Name = fileName,
            ContentType = string.IsNullOrWhiteSpace(descriptor.ContentType) ? MimeKit.MimeTypes.GetMimeType(fileName) : descriptor.ContentType,
            ContentBytes = Convert.ToBase64String(descriptor.GetContentBytes(
                Definitions.AttachmentStreamStagingOptions.DefaultMaxBytes,
                expectedLength)),
            IsInline = isInline,
            ContentId = string.IsNullOrWhiteSpace(descriptor.ContentId) ? (isInline ? fileName : null) : descriptor.ContentId
        };
    }

    internal static object PrepareInlineDescriptor(Definitions.AttachmentDescriptor descriptor) {
        if (descriptor is not Definitions.FileAttachmentDescriptor fileDescriptor) {
            return FromDescriptor(descriptor, inline: true);
        }

        return new Definitions.FileAttachmentDescriptor(fileDescriptor.FilePath) {
            FileName = fileDescriptor.FileName,
            ContentType = fileDescriptor.ContentType,
            ContentId = fileDescriptor.ContentId,
            ContentDescription = fileDescriptor.ContentDescription,
            ContentDisposition = new MimeKit.ContentDisposition(MimeKit.ContentDisposition.Inline),
            TransferEncoding = fileDescriptor.TransferEncoding,
            Headers = fileDescriptor.Headers == null
                ? null
                : new Dictionary<string, string>(fileDescriptor.Headers, StringComparer.OrdinalIgnoreCase)
        };
    }
}

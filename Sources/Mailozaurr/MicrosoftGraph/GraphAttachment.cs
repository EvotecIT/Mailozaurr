namespace Mailozaurr;

/// <summary>
/// Represents a placeholder for an attachment upload session.
/// </summary>
public class GraphAttachmentPlaceHolder {
    /// <summary>Serialized attachment metadata.</summary>
    public string Json { get; set; }
    /// <summary>Chunks of the file content.</summary>
    public List<ByteArrayContent> Content { get; set; }
    /// <summary>Size of the file in bytes.</summary>
    public long FileSize { get; set; }
    /// <summary>Name of the file.</summary>
    public string FileName { get; set; }
}

/// <summary>
/// Wrapper used when creating an upload session.
/// </summary>
public class GraphAttachmentItemWrapper {
    [JsonPropertyName("AttachmentItem")]
    public GraphAttachmentItem AttachmentItem { get; set; }

    /// <summary>Initializes a new instance of the wrapper.</summary>
    /// <param name="attachmentItem">The attachment item.</param>
    public GraphAttachmentItemWrapper(GraphAttachmentItem attachmentItem) {
        AttachmentItem = attachmentItem;
    }
}

/// <summary>
/// Metadata describing an attachment for upload.
/// </summary>
public class GraphAttachmentItem {
    [JsonPropertyName("attachmentType")]
    public string AttachmentType { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Creates a new attachment item.</summary>
    /// <param name="attachmentType">Type of the attachment.</param>
    /// <param name="name">File name.</param>
    /// <param name="size">File size.</param>
    public GraphAttachmentItem(string attachmentType, string name, long size) {
        AttachmentType = attachmentType;
        Name = name;
        Size = size;
    }
}

/// <summary>
/// Represents a simple file attachment used when sending messages.
/// </summary>
public class GraphAttachment {
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; }

    /// <summary>
    /// Creates a <see cref="GraphAttachment"/> from a local file path.
    /// </summary>
    /// <param name="filePath">Path to the file.</param>
    /// <returns>The created attachment.</returns>
    public static GraphAttachment FromFile(string filePath) {
        var fileInfo = new FileInfo(filePath);
        var fileBytes = File.ReadAllBytes(filePath);
        var fileContentBase64 = Convert.ToBase64String(fileBytes);

        return new GraphAttachment {
            Name = fileInfo.Name,
            ContentBytes = fileContentBase64
        };
    }
}
namespace Mailozaurr;

/// <summary>
/// Wrapper used when creating an upload session.
/// </summary>
public class GraphAttachmentItemWrapper {
    /// <summary>
    /// Gets the attachment item used when creating the upload session.
    /// </summary>
    [JsonPropertyName("AttachmentItem")]
    public GraphAttachmentItem AttachmentItem { get; set; }

    /// <summary>Initializes a new instance of the wrapper.</summary>
    /// <param name="attachmentItem">The attachment item.</param>
    public GraphAttachmentItemWrapper(GraphAttachmentItem attachmentItem) {
        AttachmentItem = attachmentItem;
    }
}

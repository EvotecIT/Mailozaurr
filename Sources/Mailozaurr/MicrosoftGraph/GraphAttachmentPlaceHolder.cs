namespace Mailozaurr;

/// <summary>
/// Represents a placeholder for an attachment upload session.
/// </summary>
public class GraphAttachmentPlaceHolder {
    /// <summary>Serialized attachment metadata.</summary>
    public string Json { get; set; }
    /// <summary>Chunks of the file content.</summary>
    public List<StreamContent> Content { get; set; }
    /// <summary>Path to the file.</summary>
    public string FilePath { get; set; }
    /// <summary>Size of the file in bytes.</summary>
    public long FileSize { get; set; }
    /// <summary>Name of the file.</summary>
    public string FileName { get; set; }
}

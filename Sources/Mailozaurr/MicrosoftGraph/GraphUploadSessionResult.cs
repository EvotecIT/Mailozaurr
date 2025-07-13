using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Result returned when creating an upload session for large attachments.
/// </summary>
public class GraphUploadSessionResult {
    /// <summary>
    /// Gets or sets the URL that should be used to upload the attachment bytes.
    /// </summary>
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; }
}


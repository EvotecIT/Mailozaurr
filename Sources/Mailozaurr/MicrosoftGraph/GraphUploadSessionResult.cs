using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Result returned when creating an upload session for large attachments.
/// </summary>
public class GraphUploadSessionResult {
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; }
}

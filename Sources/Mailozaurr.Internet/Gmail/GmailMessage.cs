using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents a message returned by Gmail REST API.
/// </summary>
public sealed class GmailMessage {
    /// <summary>Unique message identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Thread identifier.</summary>
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; set; }

    /// <summary>Message snippet.</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    /// <summary>Message internal date as milliseconds since epoch.</summary>
    [JsonPropertyName("internalDate")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? InternalDate { get; set; }

    /// <summary>Labels applied to the message.</summary>
    [JsonPropertyName("labelIds")]
    public List<string>? LabelIds { get; set; }

    /// <summary>Message payload.</summary>
    [JsonPropertyName("payload")]
    public GmailMessagePayload? Payload { get; set; }

    /// <summary>Raw MIME content encoded as base64url.</summary>
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }
}

/// <summary>
/// Message part/payload returned by Gmail REST API.
/// </summary>
public sealed class GmailMessagePayload {
    /// <summary>Part identifier.</summary>
    [JsonPropertyName("partId")]
    public string? PartId { get; set; }

    /// <summary>MIME type of the part.</summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    /// <summary>Filename, when present (usually indicates an attachment).</summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    /// <summary>Part headers.</summary>
    [JsonPropertyName("headers")]
    public List<GmailMessageHeader>? Headers { get; set; }

    /// <summary>Part body.</summary>
    [JsonPropertyName("body")]
    public GmailMessageBody? Body { get; set; }

    /// <summary>Child parts.</summary>
    [JsonPropertyName("parts")]
    public List<GmailMessagePayload>? Parts { get; set; }
}

/// <summary>
/// Header entry returned by Gmail REST API.
/// </summary>
public sealed class GmailMessageHeader {
    /// <summary>Header name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Header value.</summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

/// <summary>
/// Message body returned by Gmail REST API.
/// </summary>
public sealed class GmailMessageBody {
    /// <summary>Attachment identifier, when this body represents an attachment.</summary>
    [JsonPropertyName("attachmentId")]
    public string? AttachmentId { get; set; }

    /// <summary>Size of this body in bytes.</summary>
    [JsonPropertyName("size")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? Size { get; set; }

    /// <summary>Base64url-encoded data for inline bodies (often empty for attachments).</summary>
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents a mail message metadata record returned by Graph mailbox endpoints.
/// </summary>
public sealed class GraphMailMessage {
    /// <summary>Message identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Subject line.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>Received date/time in UTC.</summary>
    [JsonPropertyName("receivedDateTime")]
    public DateTimeOffset? ReceivedDateTime { get; set; }

    /// <summary>Sender.</summary>
    [JsonPropertyName("from")]
    public GraphEmailAddress? From { get; set; }

    /// <summary>Primary recipients.</summary>
    [JsonPropertyName("toRecipients")]
    public List<GraphEmailAddress>? ToRecipients { get; set; }

    /// <summary>Internet message id (RFC822 Message-Id).</summary>
    [JsonPropertyName("internetMessageId")]
    public string? InternetMessageId { get; set; }

    /// <summary>True when the message has attachments.</summary>
    [JsonPropertyName("hasAttachments")]
    public bool? HasAttachments { get; set; }

    /// <summary>True when the message is marked as read.</summary>
    [JsonPropertyName("isRead")]
    public bool? IsRead { get; set; }

    /// <summary>Message flag state.</summary>
    [JsonPropertyName("flag")]
    public GraphMailMessageFlag? Flag { get; set; }

    /// <summary>Conversation identifier.</summary>
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; set; }
}


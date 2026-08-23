using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>A server-side Gmail message filter.</summary>
public sealed class GmailFilter {
    /// <summary>Server-assigned filter identifier.</summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>Message matching criteria.</summary>
    [JsonPropertyName("criteria")]
    public GmailFilterCriteria Criteria { get; set; } = new();

    /// <summary>Actions applied to matching messages.</summary>
    [JsonPropertyName("action")]
    public GmailFilterAction Action { get; set; } = new();
}

/// <summary>Matching criteria for a Gmail filter.</summary>
public sealed class GmailFilterCriteria {
    /// <summary>Sender expression.</summary>
    [JsonPropertyName("from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? From { get; set; }

    /// <summary>Recipient expression.</summary>
    [JsonPropertyName("to")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? To { get; set; }

    /// <summary>Subject phrase.</summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subject { get; set; }

    /// <summary>Gmail search query that must match.</summary>
    [JsonPropertyName("query")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Query { get; set; }

    /// <summary>Gmail search query that must not match.</summary>
    [JsonPropertyName("negatedQuery")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NegatedQuery { get; set; }

    /// <summary>Whether the message must have an attachment.</summary>
    [JsonPropertyName("hasAttachment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasAttachment { get; set; }

    /// <summary>Whether chats are excluded.</summary>
    [JsonPropertyName("excludeChats")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ExcludeChats { get; set; }

    /// <summary>RFC822 message-size boundary in bytes.</summary>
    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; set; }

    /// <summary>Size comparison: smaller or larger.</summary>
    [JsonPropertyName("sizeComparison")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SizeComparison { get; set; }
}

/// <summary>Actions performed by a Gmail filter.</summary>
public sealed class GmailFilterAction {
    /// <summary>Label identifiers to add.</summary>
    [JsonPropertyName("addLabelIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AddLabelIds { get; set; }

    /// <summary>Label identifiers to remove.</summary>
    [JsonPropertyName("removeLabelIds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RemoveLabelIds { get; set; }

    /// <summary>Verified forwarding address.</summary>
    [JsonPropertyName("forward")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Forward { get; set; }
}

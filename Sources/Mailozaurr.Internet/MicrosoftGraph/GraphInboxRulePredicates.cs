using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents predicate options for inbox rules.
/// </summary>
public class GraphInboxRulePredicates {
    /// <summary>
    /// Gets or sets a list of strings that must appear in the sender address.
    /// </summary>
    [JsonPropertyName("senderContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SenderContains { get; set; }

    /// <summary>
    /// Gets or sets a list of strings that must appear in the recipient address.
    /// </summary>
    [JsonPropertyName("recipientContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RecipientContains { get; set; }

    /// <summary>
    /// Gets or sets a list of strings that must appear in the subject line.
    /// </summary>
    [JsonPropertyName("subjectContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SubjectContains { get; set; }

    /// <summary>
    /// Gets or sets a list of strings that must appear in the message body.
    /// </summary>
    [JsonPropertyName("bodyContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? BodyContains { get; set; }

    /// <summary>
    /// Gets or sets the required importance value.
    /// </summary>
    [JsonPropertyName("importance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Importance { get; set; }
}
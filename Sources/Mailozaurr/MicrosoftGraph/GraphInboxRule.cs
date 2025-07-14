using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents a message rule as used by Microsoft Graph.
/// </summary>
public class GraphInboxRule
{
    /// <summary>The rule identifier.</summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>Display name of the rule.</summary>
    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    /// <summary>Sequence number of the rule.</summary>
    [JsonPropertyName("sequence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Sequence { get; set; }

    /// <summary>Indicates whether the rule is enabled.</summary>
    [JsonPropertyName("isEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsEnabled { get; set; }

    /// <summary>Indicates whether the rule is read-only.</summary>
    [JsonPropertyName("isReadOnly")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsReadOnly { get; set; }

    /// <summary>Indicates whether the rule encountered errors.</summary>
    [JsonPropertyName("hasError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasError { get; set; }

    /// <summary>Conditions of the rule.</summary>
    [JsonPropertyName("conditions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphInboxRulePredicates? Conditions { get; set; }

    /// <summary>Actions executed by the rule.</summary>
    [JsonPropertyName("actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphInboxRuleActions? Actions { get; set; }

    /// <summary>Exceptions for the rule.</summary>
    [JsonPropertyName("exceptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphInboxRulePredicates? Exceptions { get; set; }
}

/// <summary>
/// Represents predicate options for inbox rules.
/// </summary>
public class GraphInboxRulePredicates
{
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

/// <summary>
/// Represents actions that an inbox rule performs.
/// </summary>
public class GraphInboxRuleActions
{
    /// <summary>
    /// Folder path the message should be moved to.
    /// </summary>
    [JsonPropertyName("moveToFolder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MoveToFolder { get; set; }

    /// <summary>
    /// Folder path the message should be copied to.
    /// </summary>
    [JsonPropertyName("copyToFolder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CopyToFolder { get; set; }

    /// <summary>
    /// Indicates whether the message should be deleted.
    /// </summary>
    [JsonPropertyName("delete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Delete { get; set; }

    /// <summary>
    /// Addresses the message should be forwarded to.
    /// </summary>
    [JsonPropertyName("forwardTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? ForwardTo { get; set; }

    /// <summary>
    /// Stops further rule processing when set.
    /// </summary>
    [JsonPropertyName("stopProcessingRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? StopProcessingRules { get; set; }
}

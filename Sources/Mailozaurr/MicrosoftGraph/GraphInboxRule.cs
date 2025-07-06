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
    [JsonPropertyName("senderContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SenderContains { get; set; }

    [JsonPropertyName("recipientContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RecipientContains { get; set; }

    [JsonPropertyName("subjectContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SubjectContains { get; set; }

    [JsonPropertyName("bodyContains")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? BodyContains { get; set; }

    [JsonPropertyName("importance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Importance { get; set; }
}

/// <summary>
/// Represents actions that an inbox rule performs.
/// </summary>
public class GraphInboxRuleActions
{
    [JsonPropertyName("moveToFolder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MoveToFolder { get; set; }

    [JsonPropertyName("copyToFolder")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CopyToFolder { get; set; }

    [JsonPropertyName("delete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Delete { get; set; }

    [JsonPropertyName("forwardTo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEmailAddress>? ForwardTo { get; set; }

    [JsonPropertyName("stopProcessingRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? StopProcessingRules { get; set; }
}

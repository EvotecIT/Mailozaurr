using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents a message rule as used by Microsoft Graph.
/// </summary>
/// <remarks>
/// Only a subset of rule properties are implemented to keep the
/// wrapper light-weight.
/// </remarks>
public class GraphInboxRule {
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
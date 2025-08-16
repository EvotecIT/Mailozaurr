using System.Text.Json.Serialization;

namespace Mailozaurr;

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

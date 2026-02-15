using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents the flag status of a Graph message.
/// </summary>
public sealed class GraphMailMessageFlag {
    /// <summary>
    /// Flag status value (for example, <c>flagged</c> or <c>notFlagged</c>).
    /// </summary>
    [JsonPropertyName("flagStatus")]
    public string? FlagStatus { get; set; }
}


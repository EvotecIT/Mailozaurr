using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents a calendar event used with Microsoft Graph.
/// </summary>
/// <remarks>
/// This type mirrors the <c>event</c> resource and exposes only
/// commonly used properties.
/// </remarks>
public class GraphEvent {
    /// <summary>Event identifier.</summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>Subject of the event.</summary>
    [JsonPropertyName("subject")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Subject { get; set; }

    /// <summary>Event start time.</summary>
    [JsonPropertyName("start")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphEventTime? Start { get; set; }

    /// <summary>Event end time.</summary>
    [JsonPropertyName("end")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphEventTime? End { get; set; }

    /// <summary>Body of the event.</summary>
    [JsonPropertyName("body")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphContent? Body { get; set; }

    /// <summary>Attendees of the event.</summary>
    [JsonPropertyName("attendees")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GraphEventAttendee>? Attendees { get; set; }
}
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents an attendee for a calendar event.
/// </summary>
/// <remarks>
/// This mirrors the <c>attendee</c> resource when creating or
/// updating events via Graph.
/// </remarks>
public class GraphEventAttendee {
    /// <summary>Email address of the attendee.</summary>
    [JsonPropertyName("emailAddress")]
    public GraphEmailAddress EmailAddress { get; set; } = new();

    /// <summary>Attendee type such as required or optional.</summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }
}

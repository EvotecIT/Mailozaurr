using System.Text.Json.Serialization;
namespace Mailozaurr;

/// <summary>
/// Represents date/time with timezone for Graph calendar events.
/// </summary>
/// <remarks>
/// Used by <see cref="GraphEvent"/> when specifying the start and
/// end times of a calendar event.
/// </remarks>
public class GraphEventTime {
    /// <summary>Date/time value in ISO 8601 format.</summary>
    [JsonPropertyName("dateTime")]
    public string DateTime { get; set; } = string.Empty;

    /// <summary>Time zone identifier.</summary>
    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; } = "UTC";
}
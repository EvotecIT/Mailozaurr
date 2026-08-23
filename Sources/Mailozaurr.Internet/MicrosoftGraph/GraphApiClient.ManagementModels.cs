using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    /// <summary>Response envelope for a Microsoft Graph inbox-rule collection.</summary>
    public sealed class GraphInboxRuleListResponse {
        /// <summary>Rules returned by the provider.</summary>
        [JsonPropertyName("value")]
        public List<GraphInboxRule>? Value { get; set; }

        /// <summary>Provider continuation URL.</summary>
        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    /// <summary>Response envelope for a Microsoft Graph calendar-event collection.</summary>
    public sealed class GraphEventListResponse {
        /// <summary>Events returned by the provider.</summary>
        [JsonPropertyName("value")]
        public List<GraphEvent>? Value { get; set; }

        /// <summary>Provider continuation URL.</summary>
        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    internal sealed class GraphInboxRuleWriteRequest {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("sequence")]
        public int? Sequence { get; set; }

        [JsonPropertyName("isEnabled")]
        public bool? IsEnabled { get; set; }

        [JsonPropertyName("conditions")]
        public GraphInboxRulePredicates? Conditions { get; set; }

        [JsonPropertyName("actions")]
        public GraphInboxRuleActions? Actions { get; set; }

        [JsonPropertyName("exceptions")]
        public GraphInboxRulePredicates? Exceptions { get; set; }

        internal static GraphInboxRuleWriteRequest From(GraphInboxRule rule) => new() {
            DisplayName = rule.DisplayName,
            Sequence = rule.Sequence,
            IsEnabled = rule.IsEnabled,
            Conditions = rule.Conditions,
            Actions = rule.Actions,
            Exceptions = rule.Exceptions
        };
    }

    internal sealed class GraphEventWriteRequest {
        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("start")]
        public GraphEventTime? Start { get; set; }

        [JsonPropertyName("end")]
        public GraphEventTime? End { get; set; }

        [JsonPropertyName("body")]
        public GraphContent? Body { get; set; }

        [JsonPropertyName("attendees")]
        public List<GraphEventAttendee>? Attendees { get; set; }

        internal static GraphEventWriteRequest From(GraphEvent graphEvent) => new() {
            Subject = graphEvent.Subject,
            Start = graphEvent.Start,
            End = graphEvent.End,
            Body = graphEvent.Body,
            Attendees = graphEvent.Attendees
        };
    }
}

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
}

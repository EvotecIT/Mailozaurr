using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    /// <summary>Create subscription request payload.</summary>
    public sealed class GraphCreateSubscriptionRequest {
        /// <summary>
        /// Resource to subscribe to (for example, <c>me/mailFolders('inbox')/messages</c>).
        /// </summary>
        [JsonPropertyName("resource")]
        public string Resource { get; set; } = string.Empty;

        /// <summary>
        /// Change types (comma-separated) to subscribe to (for example, <c>created,updated,deleted</c>).
        /// </summary>
        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; } = string.Empty;

        /// <summary>Webhook URL to receive notifications.</summary>
        [JsonPropertyName("notificationUrl")]
        public string NotificationUrl { get; set; } = string.Empty;

        /// <summary>Subscription expiration time.</summary>
        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }

        /// <summary>Optional opaque state returned in notifications.</summary>
        [JsonPropertyName("clientState")]
        public string? ClientState { get; set; }
    }

    /// <summary>Renew subscription request payload.</summary>
    public sealed class GraphRenewSubscriptionRequest {
        /// <summary>Updated expiration time.</summary>
        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }
    }

    /// <summary>Graph webhook subscription.</summary>
    public sealed class GraphSubscription {
        /// <summary>Subscription id.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Subscribed resource.</summary>
        [JsonPropertyName("resource")]
        public string? Resource { get; set; }

        /// <summary>Subscribed change types.</summary>
        [JsonPropertyName("changeType")]
        public string? ChangeType { get; set; }

        /// <summary>Webhook URL.</summary>
        [JsonPropertyName("notificationUrl")]
        public string? NotificationUrl { get; set; }

        /// <summary>Subscription expiration time.</summary>
        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }

        /// <summary>Optional opaque state returned in notifications.</summary>
        [JsonPropertyName("clientState")]
        public string? ClientState { get; set; }
    }

    /// <summary>Graph subscriptions list response.</summary>
    public sealed class GraphSubscriptionListResponse {
        /// <summary>List of subscriptions.</summary>
        [JsonPropertyName("value")]
        public List<GraphSubscription>? Value { get; set; }
    }
}
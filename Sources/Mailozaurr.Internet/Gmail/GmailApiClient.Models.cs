using MimeKit;
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

public sealed partial class GmailApiClient {
    /// <summary>Attachment metadata returned by Gmail API.</summary>
    public sealed class AttachmentResponse {
        /// <summary>Base64 encoded attachment data.</summary>
        public string? Data { get; set; }
    }

    /// <summary>Response envelope for Gmail list messages API.</summary>
    public sealed class GmailListResponse {
        /// <summary>Messages returned by the API.</summary>
        public List<GmailMessage>? Messages { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
        /// <summary>Estimated total number of results.</summary>
        [JsonPropertyName("resultSizeEstimate")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? ResultSizeEstimate { get; set; }
    }

    /// <summary>Response envelope for Gmail thread listing.</summary>
    public sealed class GmailThreadListResponse {
        /// <summary>Threads returned by the API.</summary>
        public List<GmailThreadInfo>? Threads { get; set; }
        /// <summary>Token for the next page of results.</summary>
        public string? NextPageToken { get; set; }
    }

    /// <summary>Request payload for Gmail watch API.</summary>
    public sealed class GmailWatchRequest {
        /// <summary>Pub/Sub topic name to deliver notifications to.</summary>
        [JsonPropertyName("topicName")]
        public string TopicName { get; set; } = string.Empty;
        /// <summary>Optional label filters.</summary>
        [JsonPropertyName("labelIds")]
        public List<string>? LabelIds { get; set; }
        /// <summary>Action to apply to the label filter (usually <c>include</c>).</summary>
        [JsonPropertyName("labelFilterAction")]
        public string? LabelFilterAction { get; set; }
    }

    /// <summary>Response payload for Gmail watch API.</summary>
    public sealed class GmailWatchResponse {
        /// <summary>History id at the start of the watch.</summary>
        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }
        /// <summary>Watch expiration as milliseconds since epoch.</summary>
        [JsonPropertyName("expiration")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long Expiration { get; set; }
    }

    /// <summary>Gmail profile response.</summary>
    public sealed class GmailProfile {
        /// <summary>Email address associated with the mailbox.</summary>
        [JsonPropertyName("emailAddress")]
        public string? EmailAddress { get; set; }
        /// <summary>Total number of messages.</summary>
        [JsonPropertyName("messagesTotal")]
        public long MessagesTotal { get; set; }
        /// <summary>Total number of threads.</summary>
        [JsonPropertyName("threadsTotal")]
        public long ThreadsTotal { get; set; }
        /// <summary>Current history id.</summary>
        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }
    }

    /// <summary>Gmail history list response.</summary>
    public sealed class GmailHistoryListResponse {
        /// <summary>History records.</summary>
        [JsonPropertyName("history")]
        public List<GmailHistoryRecord>? History { get; set; }
        /// <summary>Token for the next page of results.</summary>
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; set; }
        /// <summary>Latest history id.</summary>
        [JsonPropertyName("historyId")]
        public string? HistoryId { get; set; }
    }

    /// <summary>History record returned by Gmail history API.</summary>
    public sealed class GmailHistoryRecord {
        /// <summary>History record id.</summary>
        public string? Id { get; set; }
        /// <summary>Messages added in this history record.</summary>
        public List<GmailHistoryMessageAdded>? MessagesAdded { get; set; }
        /// <summary>Messages deleted in this history record.</summary>
        public List<GmailHistoryMessageDeleted>? MessagesDeleted { get; set; }
        /// <summary>Labels added in this history record.</summary>
        public List<GmailHistoryLabelAdded>? LabelsAdded { get; set; }
        /// <summary>Labels removed in this history record.</summary>
        public List<GmailHistoryLabelRemoved>? LabelsRemoved { get; set; }
    }

    /// <summary>History wrapper for a message added event.</summary>
    public sealed class GmailHistoryMessageAdded {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
    }

    /// <summary>History wrapper for a message deleted event.</summary>
    public sealed class GmailHistoryMessageDeleted {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
    }

    /// <summary>History wrapper for a label added event.</summary>
    public sealed class GmailHistoryLabelAdded {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
        /// <summary>Label ids associated with the event.</summary>
        public List<string>? LabelIds { get; set; }
    }

    /// <summary>History wrapper for a label removed event.</summary>
    public sealed class GmailHistoryLabelRemoved {
        /// <summary>Message reference associated with the event.</summary>
        public GmailHistoryMessageRef? Message { get; set; }
        /// <summary>Label ids associated with the event.</summary>
        public List<string>? LabelIds { get; set; }
    }

    /// <summary>Reference to a Gmail message returned by the history API.</summary>
    public sealed class GmailHistoryMessageRef {
        /// <summary>Message id.</summary>
        public string? Id { get; set; }
        /// <summary>Thread id.</summary>
        public string? ThreadId { get; set; }
    }

    /// <summary>Response envelope for Gmail list labels API.</summary>
    public sealed class GmailLabelListResponse {
        /// <summary>Labels returned by the API.</summary>
        public List<GmailLabel>? Labels { get; set; }
    }

    /// <summary>Request payload for Gmail modify label endpoints.</summary>
    public sealed class GmailModifyLabelsRequest {
        /// <summary>Label ids to add.</summary>
        [JsonPropertyName("addLabelIds")]
        public IReadOnlyCollection<string> AddLabelIds { get; set; } = Array.Empty<string>();

        /// <summary>Label ids to remove.</summary>
        [JsonPropertyName("removeLabelIds")]
        public IReadOnlyCollection<string> RemoveLabelIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>Request payload for Gmail batch modify messages endpoint.</summary>
    public sealed class GmailBatchModifyRequest {
        /// <summary>Message ids.</summary>
        [JsonPropertyName("ids")]
        public IReadOnlyCollection<string> Ids { get; set; } = Array.Empty<string>();

        /// <summary>Label ids to add.</summary>
        [JsonPropertyName("addLabelIds")]
        public IReadOnlyCollection<string> AddLabelIds { get; set; } = Array.Empty<string>();

        /// <summary>Label ids to remove.</summary>
        [JsonPropertyName("removeLabelIds")]
        public IReadOnlyCollection<string> RemoveLabelIds { get; set; } = Array.Empty<string>();
    }

    /// <summary>Request payload for Gmail batch delete messages endpoint.</summary>
    public sealed class GmailBatchDeleteRequest {
        /// <summary>Message ids.</summary>
        [JsonPropertyName("ids")]
        public IReadOnlyCollection<string> Ids { get; set; } = Array.Empty<string>();
    }

    /// <summary>Request payload for Gmail import message endpoint.</summary>
    public sealed class GmailImportMessageRequest {
        /// <summary>Raw message content as base64url.</summary>
        [JsonPropertyName("raw")]
        public string Raw { get; set; } = string.Empty;

        /// <summary>Optional label ids to apply to the imported message.</summary>
        [JsonPropertyName("labelIds")]
        public List<string>? LabelIds { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphMailboxBrowser {
    /// <summary>
    /// Graph mailbox folder summary.
    /// </summary>
    public sealed class GraphMailboxFolderSummary {
        /// <summary>Graph folder identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Hierarchical display path (for example, <c>Inbox/Projects</c>).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Folder display name.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Parent folder id, when available.</summary>
        public string? ParentId { get; set; }

        /// <summary>Graph well-known folder name, when available.</summary>
        public string? WellKnownName { get; set; }

        /// <summary>Total item count, when available.</summary>
        public int? TotalItemCount { get; set; }

        /// <summary>Unread item count, when available.</summary>
        public int? UnreadItemCount { get; set; }
    }

    /// <summary>
    /// Graph mailbox list result.
    /// </summary>
    public sealed class GraphMailboxListResult {
        /// <summary>Resolved Graph folder selector used for listing.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Total item count as reported by Graph folder metadata.</summary>
        public int TotalCount { get; set; }

        /// <summary>Message summaries.</summary>
        public List<GraphMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox conversation list result.
    /// </summary>
    public sealed class GraphMailboxConversationListResult {
        /// <summary>Graph conversation id used for listing.</summary>
        public string ConversationId { get; set; } = string.Empty;

        /// <summary>Total number of messages available in the conversation slice source.</summary>
        public int TotalCount { get; set; }

        /// <summary>Paged conversation message summaries.</summary>
        public List<GraphMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox search request.
    /// </summary>
    public sealed class GraphMailboxSearchRequest {
        /// <summary>Folder filter.</summary>
        public string? Folder { get; set; }

        /// <summary>Free-form Graph search query string.</summary>
        public string? Query { get; set; }

        /// <summary>Subject contains filter.</summary>
        public string? SubjectContains { get; set; }

        /// <summary>From contains filter.</summary>
        public string? FromContains { get; set; }

        /// <summary>To contains filter.</summary>
        public string? ToContains { get; set; }

        /// <summary>Body contains filter.</summary>
        public string? BodyContains { get; set; }

        /// <summary>Unseen-only filter.</summary>
        public bool UnseenOnly { get; set; }

        /// <summary>Has-attachment filter.</summary>
        public bool HasAttachment { get; set; }

        /// <summary>Lower bound for received date/time (UTC).</summary>
        public DateTime? SinceUtc { get; set; }

        /// <summary>Upper bound for received date/time (UTC).</summary>
        public DateTime? BeforeUtc { get; set; }
    }

    /// <summary>
    /// Graph mailbox search result.
    /// </summary>
    public sealed class GraphMailboxSearchResult {
        /// <summary>Resolved Graph folder selector used for search.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Matched messages.</summary>
        public List<GraphMailboxMessageSummary> Messages { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox import result.
    /// </summary>
    public sealed class GraphMailboxImportResult {
        /// <summary>Resolved Graph folder selector used for import.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Created Graph native message id.</summary>
        public string? NativeId { get; set; }

        /// <summary>Normalized RFC822 Message-Id used for import.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Graph mailbox send result.
    /// </summary>
    public sealed class GraphMailboxSendResult {
        /// <summary>Graph draft id that was sent.</summary>
        public string? DraftId { get; set; }

        /// <summary>Normalized RFC822 Message-Id used for send.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Graph mailbox duplicate probe result.
    /// </summary>
    public sealed class GraphMailboxDuplicateProbeResult {
        /// <summary>True when a matching message was found.</summary>
        public bool IsMatch { get; set; }

        /// <summary>Resolved Graph folder selector used for probing.</summary>
        public string? FolderSelector { get; set; }

        /// <summary>Matched Graph native message id, when available.</summary>
        public string? NativeId { get; set; }

        /// <summary>Matched normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Graph mailbox webhook subscription result.
    /// </summary>
    public sealed class GraphMailboxSubscriptionResult {
        /// <summary>Graph subscription id.</summary>
        public string? SubscriptionId { get; set; }

        /// <summary>Subscription resource path.</summary>
        public string? Resource { get; set; }

        /// <summary>Client-state value when provided.</summary>
        public string? ClientState { get; set; }

        /// <summary>Subscription expiration value.</summary>
        public DateTimeOffset ExpirationDateTime { get; set; }
    }

    /// <summary>
    /// Graph mailbox webhook delete result.
    /// </summary>
    public sealed class GraphMailboxSubscriptionDeleteResult {
        /// <summary>True when delete operation succeeded.</summary>
        public bool Deleted { get; set; }

        /// <summary>True when delete succeeded because subscription was already gone.</summary>
        public bool AlreadyDeleted { get; set; }
    }

    /// <summary>
    /// Graph mailbox webhook renew result.
    /// </summary>
    public sealed class GraphMailboxSubscriptionRenewResult {
        /// <summary>True when renew operation succeeded.</summary>
        public bool Renewed { get; set; }

        /// <summary>True when renew failed because subscription was already missing.</summary>
        public bool Missing { get; set; }

        /// <summary>Renewed subscription payload when available.</summary>
        public GraphMailboxSubscriptionResult? Subscription { get; set; }
    }

    /// <summary>
    /// Graph mailbox threading metadata.
    /// </summary>
    public sealed class GraphMailboxThreadingMetadataResult {
        /// <summary>Normalized RFC822 Message-Id.</summary>
        public string? MessageId { get; set; }

        /// <summary>Reply-To header value.</summary>
        public string? ReplyTo { get; set; }

        /// <summary>Cc header value.</summary>
        public string? Cc { get; set; }

        /// <summary>Normalized RFC822 In-Reply-To value.</summary>
        public string? InReplyTo { get; set; }

        /// <summary>Normalized RFC822 References tokens.</summary>
        public List<string> References { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox delta result.
    /// </summary>
    public sealed class GraphMailboxDeltaResult {
        /// <summary>Resolved Graph folder selector used for delta query.</summary>
        public string FolderSelector { get; set; } = string.Empty;

        /// <summary>Graph delta cursor (next or delta link).</summary>
        public string? Cursor { get; set; }

        /// <summary>Messages to upsert.</summary>
        public List<GraphMailboxMessageSummary> Upserts { get; set; } = new();

        /// <summary>Deleted message ids.</summary>
        public List<string> DeletedNativeIds { get; set; } = new();
    }

    /// <summary>
    /// Graph mailbox get-message result.
    /// </summary>
    public sealed class GraphMailboxGetResult {
        /// <summary>Parsed MIME message.</summary>
        public MimeMessage Message { get; set; } = new MimeMessage();

        /// <summary>Read state from Graph metadata.</summary>
        public bool? Seen { get; set; }

        /// <summary>Flagged state from Graph metadata.</summary>
        public bool? Flagged { get; set; }

        /// <summary>Graph conversation id.</summary>
        public string? NativeThreadId { get; set; }
    }

    /// <summary>
    /// Provider-agnostic Graph mailbox message summary.
    /// </summary>
    public sealed class GraphMailboxMessageSummary {
        /// <summary>Graph message id.</summary>
        public string NativeId { get; set; } = string.Empty;

        /// <summary>Graph conversation id.</summary>
        public string? NativeThreadId { get; set; }

        /// <summary>Normalized message-id header value.</summary>
        public string? MessageId { get; set; }

        /// <summary>Sender address.</summary>
        public string From { get; set; } = string.Empty;

        /// <summary>Joined recipient list.</summary>
        public string To { get; set; } = string.Empty;

        /// <summary>Subject line.</summary>
        public string? Subject { get; set; }

        /// <summary>Message received date/time (UTC).</summary>
        public DateTime DateUtc { get; set; }

        /// <summary>True when message has attachments.</summary>
        public bool HasAttachments { get; set; }

        /// <summary>True when message is seen.</summary>
        public bool Seen { get; set; }

        /// <summary>True when message is flagged.</summary>
        public bool Flagged { get; set; }
    }
}
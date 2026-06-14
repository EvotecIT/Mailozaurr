using MimeKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// High-level delegated-token mailbox browsing helpers built on top of <see cref="GmailApiClient"/>.
/// </summary>
public sealed partial class GmailMailboxBrowser {
    private const int BatchMaxIds = 1000;
    private const string ListFields = "messages(id,threadId),nextPageToken,resultSizeEstimate";
    private const string MessageSummaryFields = "id,threadId,internalDate,labelIds,payload(headers,name,value,parts,filename,body/attachmentId,body/size,mimeType)";
    private const string ThreadFields = "id,messages(id,threadId,internalDate,labelIds,payload(headers,name,value,parts,filename,body/attachmentId,body/size,mimeType))";
    private const string RawFields = "id,threadId,internalDate,labelIds,raw";
    private readonly GmailApiClient _gmail;
    private readonly string _userId;

    /// <summary>
    /// Maximum MIME payload size used by <see cref="GetMessageContentAsync"/> when no explicit limit is provided.
    /// </summary>
    public const int DefaultMaxMimeBytes = 25 * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailMailboxBrowser"/> class.
    /// </summary>
    /// <param name="gmail">Gmail API client.</param>
    /// <param name="userId">Mailbox user id (use <c>me</c> for delegated tokens).</param>
    public GmailMailboxBrowser(GmailApiClient gmail, string userId = "me") {
        _gmail = gmail ?? throw new ArgumentNullException(nameof(gmail));
        _userId = string.IsNullOrWhiteSpace(userId) ? "me" : userId.Trim();
    }

    /// <summary>
    /// Resolves a folder/label selector to a Gmail label id.
    /// </summary>
    public async Task<string?> ResolveLabelIdAsync(
        string? folder,
        CancellationToken cancellationToken = default) {
        var raw = (folder ?? string.Empty).Trim();
        if (raw.Length == 0) {
            return "INBOX";
        }

        if (TryResolveSystemLabel(raw, out var systemLabelId)) {
            return systemLabelId;
        }

        var labels = await _gmail.ListLabelsAsync(_userId, cancellationToken).ConfigureAwait(false);
        foreach (var label in labels) {
            if (label == null) {
                continue;
            }

            var id = NormalizeOptional(label.Id);
            var name = NormalizeOptional(label.Name);
            if (id == null || name == null) {
                continue;
            }
            if (id.Equals(raw, StringComparison.OrdinalIgnoreCase) || name.Equals(raw, StringComparison.OrdinalIgnoreCase)) {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a folder selector for Gmail watch requests.
    /// </summary>
    public async Task<string?> ResolveWatchLabelIdAsync(
        string folder,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(folder)) {
            return null;
        }

        if (TryResolveWatchSystemLabel(folder, out var watchLabelId)) {
            return watchLabelId;
        }

        return await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists available Gmail labels as mailbox folders.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxFolderSummary>> ListFoldersAsync(
        CancellationToken cancellationToken = default) {
        var labels = await _gmail.ListLabelsAsync(_userId, cancellationToken).ConfigureAwait(false);
        if (labels == null || labels.Count == 0) {
            return Array.Empty<GmailMailboxFolderSummary>();
        }

        var output = new List<GmailMailboxFolderSummary>(labels.Count);
        foreach (var label in labels) {
            if (label == null) {
                continue;
            }

            var id = NormalizeOptional(label.Id);
            var name = NormalizeOptional(label.Name);
            if (id == null || name == null) {
                continue;
            }

            output.Add(new GmailMailboxFolderSummary {
                Id = id,
                Name = name,
                Type = NormalizeOptional(label.Type)
            });
        }

        output.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return output;
    }

    /// <summary>
    /// Lists messages in a Gmail label.
    /// </summary>
    public async Task<GmailMailboxListResult> ListMessagesAsync(
        string? folder,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) {
        var safeLimit = ClampInt(limit, 1, 2000);
        var skipRemaining = Math.Max(0, offset);

        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        string? pageToken = null;
        long totalEstimate = 0;
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var selectedIds = new List<string>();

        while (selectedIds.Count < safeLimit) {
            var page = await _gmail.ListPageAsync(
                _userId,
                query: null,
                labelIds: new[] { resolvedLabelId },
                includeSpamTrash: false,
                maxResults: 100,
                pageToken: pageToken,
                fields: ListFields,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (page.ResultSizeEstimate.HasValue && page.ResultSizeEstimate.Value > 0) {
                totalEstimate = page.ResultSizeEstimate.Value;
            }

            pageToken = page.NextPageToken;
            if (page.Messages == null || page.Messages.Count == 0) {
                break;
            }

            foreach (var message in page.Messages) {
                var id = NormalizeOptional(message?.Id);
                if (id == null || !seenIds.Add(id)) {
                    continue;
                }

                if (skipRemaining > 0) {
                    skipRemaining--;
                    continue;
                }

                selectedIds.Add(id);
                if (selectedIds.Count >= safeLimit) {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(pageToken)) {
                break;
            }
        }

        var summaries = new List<GmailMailboxMessageSummary>(selectedIds.Count);
        foreach (var id in selectedIds) {
            var summary = await TryGetMessageSummaryAsync(id, cancellationToken).ConfigureAwait(false);
            if (summary != null) {
                summaries.Add(summary);
            }
        }

        return new GmailMailboxListResult {
            ResolvedLabelId = resolvedLabelId,
            TotalCount = totalEstimate > int.MaxValue ? int.MaxValue : (int)totalEstimate,
            Messages = summaries
        };
    }

    /// <summary>
    /// Lists messages in a Gmail thread.
    /// </summary>
    public async Task<IReadOnlyList<GmailMailboxMessageSummary>> ListThreadMessagesAsync(
        string threadId,
        int maxItems = 2000,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(threadId)) {
            throw new ArgumentException("threadId is required.", nameof(threadId));
        }

        var thread = await _gmail.GetThreadWithOptionsAsync(
            _userId,
            threadId.Trim(),
            format: "full",
            fields: ThreadFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var summaries = MapSummaries(thread.Messages);
        summaries.Sort(static (a, b) => b.DateUtc.CompareTo(a.DateUtc));
        var cap = ClampInt(maxItems, 1, 10000);
        if (summaries.Count > cap) {
            summaries = summaries.GetRange(0, cap);
        }

        return summaries;
    }

    /// <summary>
    /// Lists a paged slice of messages in a Gmail thread.
    /// </summary>
    public async Task<GmailMailboxThreadListResult> ListThreadMessagesPageAsync(
        string threadId,
        int limit,
        int offset,
        int maxItems = 2000,
        CancellationToken cancellationToken = default) {
        var messages = await ListThreadMessagesAsync(
            threadId,
            maxItems: maxItems,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var total = messages.Count;
        var skip = Math.Max(0, offset);
        var take = ClampInt(limit, 1, 1000);
        var page = messages.Skip(skip).Take(take).ToList();

        return new GmailMailboxThreadListResult {
            ThreadId = threadId.Trim(),
            TotalCount = total,
            Messages = page
        };
    }

    /// <summary>
    /// Searches messages in a Gmail label.
    /// </summary>
    public async Task<GmailMailboxSearchResult> SearchMessagesAsync(
        GmailMailboxSearchRequest request,
        int max,
        CancellationToken cancellationToken = default) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var safeMax = ClampInt(max, 1, 2000);
        var resolvedLabelId = NormalizeOptional(await ResolveLabelIdAsync(request.Folder, cancellationToken).ConfigureAwait(false));
        if (resolvedLabelId == null) {
            throw new InvalidOperationException("Unable to resolve Gmail folder/label.");
        }

        var query = BuildSearchQuery(request);
        var selected = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        string? pageToken = null;

        while (selected.Count < safeMax) {
            var page = await _gmail.ListPageAsync(
                _userId,
                query: string.IsNullOrWhiteSpace(query) ? null : query,
                labelIds: new[] { resolvedLabelId },
                includeSpamTrash: false,
                maxResults: 100,
                pageToken: pageToken,
                fields: ListFields,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            pageToken = page.NextPageToken;
            if (page.Messages == null || page.Messages.Count == 0) {
                break;
            }

            foreach (var message in page.Messages) {
                var id = NormalizeOptional(message?.Id);
                if (id == null || !seenIds.Add(id)) {
                    continue;
                }

                selected.Add(id);
                if (selected.Count >= safeMax) {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(pageToken)) {
                break;
            }
        }

        var summaries = new List<GmailMailboxMessageSummary>(selected.Count);
        foreach (var id in selected) {
            var summary = await TryGetMessageSummaryAsync(id, cancellationToken).ConfigureAwait(false);
            if (summary != null) {
                summaries.Add(summary);
            }
        }

        summaries.Sort(static (a, b) => b.DateUtc.CompareTo(a.DateUtc));
        return new GmailMailboxSearchResult {
            ResolvedLabelId = resolvedLabelId,
            Messages = summaries
        };
    }

    /// <summary>
    /// Builds a Gmail query string from mailbox search filters.
    /// </summary>
    public static string BuildSearchQuery(GmailMailboxSearchRequest request) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var parts = new List<string>();
        void Add(string? value) {
            var normalized = NormalizeOptional(value);
            if (normalized != null) {
                parts.Add(normalized);
            }
        }

        if (request.UnseenOnly) {
            parts.Add("is:unread");
        }
        if (request.HasAttachment) {
            parts.Add("has:attachment");
        }

        if (request.SinceUtc.HasValue) {
            var sinceUtc = DateTime.SpecifyKind(request.SinceUtc.Value, DateTimeKind.Utc);
            var after = new DateTimeOffset(sinceUtc).ToUnixTimeSeconds();
            parts.Add("after:" + after.ToString(CultureInfo.InvariantCulture));
        }
        if (request.BeforeUtc.HasValue) {
            var beforeUtc = DateTime.SpecifyKind(request.BeforeUtc.Value, DateTimeKind.Utc);
            var before = new DateTimeOffset(beforeUtc).ToUnixTimeSeconds();
            parts.Add("before:" + before.ToString(CultureInfo.InvariantCulture));
        }

        var subjectContains = NormalizeOptional(request.SubjectContains);
        if (!string.IsNullOrWhiteSpace(subjectContains)) {
            Add("subject:(" + subjectContains + ")");
        }
        var fromContains = NormalizeOptional(request.FromContains);
        if (!string.IsNullOrWhiteSpace(fromContains)) {
            Add("from:(" + fromContains + ")");
        }
        var toContains = NormalizeOptional(request.ToContains);
        if (!string.IsNullOrWhiteSpace(toContains)) {
            Add("to:(" + toContains + ")");
        }
        if (!string.IsNullOrWhiteSpace(request.BodyContains)) {
            Add(request.BodyContains);
        }
        if (!string.IsNullOrWhiteSpace(request.Query)) {
            Add(request.Query);
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets one message summary.
    /// </summary>
    public async Task<GmailMailboxMessageSummary> GetMessageSummaryAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var message = await _gmail.GetFullAsync(
            _userId,
            messageId.Trim(),
            fields: MessageSummaryFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return MapSummary(message);
    }

    /// <summary>
    /// Imports a MIME message into Gmail with a target label (typically <c>SENT</c>).
    /// </summary>
    /// <param name="message">MIME message to import.</param>
    /// <param name="labelId">Target label id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Import result.</returns>
    public async Task<GmailMailboxImportResult> ImportMessageAsync(
        MimeMessage message,
        string labelId = "SENT",
        CancellationToken cancellationToken = default) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (string.IsNullOrWhiteSpace(labelId)) {
            throw new ArgumentException("labelId is required.", nameof(labelId));
        }

        string raw;
        using (var ms = new MemoryStream()) {
            await message.WriteToAsync(ms, cancellationToken).ConfigureAwait(false);
            raw = Base64UrlEncode(ms.ToArray());
        }

        var imported = await _gmail.ImportAsync(
            _userId,
            raw,
            labelIds: new[] { labelId.Trim() },
            internalDateSource: "dateHeader",
            neverMarkSpam: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new GmailMailboxImportResult {
            LabelId = labelId.Trim(),
            NativeId = NormalizeOptional(imported.Id),
            NativeThreadId = NormalizeOptional(imported.ThreadId)
        };
    }

    /// <summary>
    /// Sends a MIME message through Gmail.
    /// </summary>
    /// <param name="message">MIME message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Send result metadata.</returns>
    public async Task<GmailMailboxSendResult> SendMessageAsync(
        MimeMessage message,
        CancellationToken cancellationToken = default) {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var sent = await _gmail.SendAsync(_userId, message, cancellationToken).ConfigureAwait(false);
        return new GmailMailboxSendResult {
            NativeId = NormalizeOptional(sent.Id),
            NativeThreadId = NormalizeOptional(sent.ThreadId)
        };
    }

    /// <summary>
    /// Probes a Gmail label for a message with a matching RFC822 <c>Message-Id</c> token.
    /// </summary>
    /// <param name="messageIdToken">Message-Id token (with or without angle brackets).</param>
    /// <param name="sentLabelId">Label id to probe. Defaults to <c>SENT</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Duplicate probe result.</returns>
    public async Task<GmailMailboxDuplicateProbeResult> FindSentMessageByRfc822MessageIdAsync(
        string messageIdToken,
        string sentLabelId = "SENT",
        CancellationToken cancellationToken = default) {
        var normalizedToken = NormalizeMessageIdValue(messageIdToken);
        if (normalizedToken == null) {
            throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        }

        var query = "rfc822msgid:" + normalizedToken;
        var page = await _gmail.ListPageAsync(
            _userId,
            query: query,
            labelIds: new[] { sentLabelId },
            includeSpamTrash: false,
            maxResults: 1,
            pageToken: null,
            fields: ListFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var first = page.Messages?.FirstOrDefault();
        if (first == null) {
            return new GmailMailboxDuplicateProbeResult {
                IsMatch = false,
                LabelId = sentLabelId
            };
        }

        return new GmailMailboxDuplicateProbeResult {
            IsMatch = true,
            LabelId = sentLabelId,
            NativeId = NormalizeOptional(first.Id),
            NativeThreadId = NormalizeOptional(first.ThreadId),
            MessageId = normalizedToken
        };
    }

    /// <summary>
    /// Reads provider threading metadata for a single message.
    /// </summary>
    /// <param name="messageId">Gmail message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Threading metadata parsed from selected headers.</returns>
    public async Task<GmailMailboxThreadingMetadataResult> GetThreadingMetadataAsync(
        string messageId,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }

        var message = await _gmail.GetMessageWithOptionsAsync(
            _userId,
            messageId.Trim(),
            format: "metadata",
            metadataHeaders: new[] { "Message-ID", "In-Reply-To", "References", "Reply-To", "Cc" },
            fields: "id,payload(headers)",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var headers = message.Payload?.Headers;
        if (headers == null || headers.Count == 0) {
            return new GmailMailboxThreadingMetadataResult();
        }

        string? messageIdHeader = null;
        string? inReplyTo = null;
        string? referencesRaw = null;
        string? replyTo = null;
        string? cc = null;

        foreach (var header in headers) {
            var name = header?.Name;
            var value = header?.Value;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            var headerName = name!.Trim();
            var headerValue = value!.Trim();
            if (headerName.Equals("Message-ID", StringComparison.OrdinalIgnoreCase)) {
                messageIdHeader = NormalizeMessageIdValue(headerValue);
            } else if (headerName.Equals("In-Reply-To", StringComparison.OrdinalIgnoreCase)) {
                inReplyTo = NormalizeMessageIdValue(headerValue);
            } else if (headerName.Equals("References", StringComparison.OrdinalIgnoreCase)) {
                referencesRaw = headerValue;
            } else if (headerName.Equals("Reply-To", StringComparison.OrdinalIgnoreCase)) {
                replyTo = NormalizeOptional(headerValue);
            } else if (headerName.Equals("Cc", StringComparison.OrdinalIgnoreCase)) {
                cc = NormalizeOptional(headerValue);
            }
        }

        return new GmailMailboxThreadingMetadataResult {
            MessageId = messageIdHeader,
            ReplyTo = replyTo,
            Cc = cc,
            InReplyTo = inReplyTo,
            References = SplitMessageIdTokens(referencesRaw)
        };
    }

    /// <summary>
    /// Gets one message content by downloading MIME payload and parsing it to <see cref="MimeMessage"/>.
    /// </summary>
    public async Task<GmailMailboxGetResult> GetMessageContentAsync(
        string messageId,
        int maxMimeBytes = DefaultMaxMimeBytes,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(messageId)) {
            throw new ArgumentException("messageId is required.", nameof(messageId));
        }
        if (maxMimeBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxMimeBytes), "maxMimeBytes must be greater than zero.");
        }

        var message = await _gmail.GetRawAsync(
            _userId,
            messageId.Trim(),
            fields: RawFields,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(message.Raw)) {
            throw new InvalidDataException("Gmail message response did not contain raw RFC822 payload.");
        }

        byte[] mimeBytes;
        try {
            mimeBytes = Base64UrlDecode(message.Raw!);
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to decode Gmail raw MIME payload.", ex);
        }

        if (mimeBytes.Length > maxMimeBytes) {
            throw new InvalidOperationException("Gmail raw message exceeds " + maxMimeBytes.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }

        MimeMessage mimeMessage;
        try {
            mimeMessage = MimeMessage.Load(new MemoryStream(mimeBytes, writable: false));
        } catch (Exception ex) {
            throw new InvalidDataException("Failed to parse Gmail MIME message.", ex);
        }

        return new GmailMailboxGetResult {
            Message = mimeMessage,
            Seen = !HasLabel(message.LabelIds, "UNREAD"),
            Flagged = HasLabel(message.LabelIds, "STARRED"),
            NativeThreadId = NormalizeOptional(message.ThreadId)
        };
    }




}
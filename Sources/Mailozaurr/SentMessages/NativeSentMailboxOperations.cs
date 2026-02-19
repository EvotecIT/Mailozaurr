#pragma warning disable CS1591
#pragma warning disable CS8600,CS8601,CS8602,CS8603,CS8604,CS8618,CS8625
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr;
using MimeKit;

namespace Mailozaurr;

public static class NativeSentMailboxOperations {
    private const int GraphUploadChunkSize = 5 * 1024 * 1024;

    public sealed class NativeSentAppendResult {
        public bool Appended { get; init; }
        public string? Folder { get; init; }
    }

    public sealed class NativeSentDuplicateProbeResult {
        public bool IsMatch { get; init; }
        public string? Folder { get; init; }
        public string? MessageId { get; init; }
    }

    public static async Task<NativeSentAppendResult> AppendToGraphSentAsync(
        GraphMailboxBrowser browser,
        MimeMessage message,
        string? requestedSentFolder,
        string? configuredSentFolder,
        int maxInlineAttachmentBytes,
        string? idempotencyHeaderName,
        CancellationToken cancellationToken) {
        if (browser is null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        var graph = GetGraphApiClient(browser);
        var resolvedFolder = ResolveGraphFolder(requestedSentFolder, configuredSentFolder, fallback: "sentitems");
        var prepared = GraphMimePreparation.PrepareMessage(message, maxInlineAttachmentBytes, idempotencyHeaderName);
        try {
            var created = await graph.CreateMessageAsync(
                prepared.Message,
                folderIdOrWellKnownName: resolvedFolder,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (prepared.UploadAttachments.Count > 0 && !string.IsNullOrWhiteSpace(created.Id)) {
                foreach (var attachment in prepared.UploadAttachments) {
                    await UploadGraphAttachmentAsync(graph, created.Id!, attachment, cancellationToken).ConfigureAwait(false);
                }
            }

            return new NativeSentAppendResult {
                Appended = true,
                Folder = resolvedFolder
            };
        } finally {
            foreach (var attachment in prepared.UploadAttachments) {
                attachment.Dispose();
            }
        }
    }

    public static async Task<NativeSentAppendResult> AppendToGmailSentAsync(
        GmailMailboxBrowser browser,
        MimeMessage message,
        string? requestedSentFolder,
        string? configuredSentFolder,
        CancellationToken cancellationToken) {
        if (browser is null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (message is null) {
            throw new ArgumentNullException(nameof(message));
        }

        var gmail = GetGmailApiClient(browser);
        var userId = GetGmailUserId(browser);
        var folder = ResolveGmailFolder(requestedSentFolder, configuredSentFolder, fallback: "SENT");
        var labelId = await browser.ResolveLabelIdAsync(folder, cancellationToken).ConfigureAwait(false);
        var labels = string.IsNullOrWhiteSpace(labelId) ? new[] { "SENT" } : new[] { labelId.Trim() };
        var raw = EncodeMimeAsBase64Url(message);
        await gmail.ImportAsync(userId, raw, labelIds: labels, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new NativeSentAppendResult {
            Appended = true,
            Folder = folder
        };
    }

    public static async Task<NativeSentDuplicateProbeResult> FindGraphSentDuplicateAsync(
        GraphMailboxBrowser browser,
        string messageIdToken,
        string? requestedSentFolder,
        string? configuredSentFolder,
        CancellationToken cancellationToken) {
        if (browser is null) {
            throw new ArgumentNullException(nameof(browser));
        }

        var normalized = NormalizeMessageIdToken(messageIdToken);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return new NativeSentDuplicateProbeResult { IsMatch = false };
        }

        var folder = ResolveGraphFolder(requestedSentFolder, configuredSentFolder, fallback: "sentitems");
        var search = await browser.SearchMessagesAsync(new GraphMailboxBrowser.GraphMailboxSearchRequest {
            Folder = folder,
            Query = normalized
        }, max: 25, cancellationToken: cancellationToken).ConfigureAwait(false);

        var match = search.Messages.FirstOrDefault(m =>
            string.Equals(NormalizeMessageIdToken(m.MessageId), normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null) {
            return new NativeSentDuplicateProbeResult { IsMatch = false };
        }

        return new NativeSentDuplicateProbeResult {
            IsMatch = true,
            Folder = folder,
            MessageId = NormalizeMessageIdToken(match.MessageId) ?? normalized
        };
    }

    public static async Task<NativeSentDuplicateProbeResult> FindGmailSentDuplicateAsync(
        GmailMailboxBrowser browser,
        string messageIdToken,
        string? requestedSentFolder,
        string? configuredSentFolder,
        CancellationToken cancellationToken) {
        if (browser is null) {
            throw new ArgumentNullException(nameof(browser));
        }

        var normalized = NormalizeMessageIdToken(messageIdToken);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return new NativeSentDuplicateProbeResult { IsMatch = false };
        }

        var folder = ResolveGmailFolder(requestedSentFolder, configuredSentFolder, fallback: "SENT");
        var search = await browser.SearchMessagesAsync(new GmailMailboxBrowser.GmailMailboxSearchRequest {
            Folder = folder,
            Query = "rfc822msgid:" + normalized
        }, max: 25, cancellationToken: cancellationToken).ConfigureAwait(false);

        var match = search.Messages.FirstOrDefault(m =>
            string.Equals(NormalizeMessageIdToken(m.MessageId), normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null) {
            return new NativeSentDuplicateProbeResult { IsMatch = false };
        }

        return new NativeSentDuplicateProbeResult {
            IsMatch = true,
            Folder = search.ResolvedLabelId,
            MessageId = NormalizeMessageIdToken(match.MessageId) ?? normalized
        };
    }

    private static async Task UploadGraphAttachmentAsync(
        GraphApiClient graph,
        string messageId,
        DecodedMimeAttachment attachment,
        CancellationToken cancellationToken) {
        var item = new GraphAttachmentItem("file", attachment.Name, attachment.Length) {
            ContentType = attachment.ContentType,
            IsInline = attachment.IsInline,
            ContentId = attachment.ContentId
        };

        var session = await graph.CreateAttachmentUploadSessionAsync(
            messageId,
            item,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        using var stream = attachment.OpenRead();
        var buffer = new byte[GraphUploadChunkSize];
        long offset = 0;
        while (offset < attachment.Length) {
            var remaining = attachment.Length - offset;
            var readLength = (int)Math.Min(buffer.Length, remaining);
#if NET5_0_OR_GREATER
            var read = await stream.ReadAsync(buffer.AsMemory(0, readLength), cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(buffer, 0, readLength, cancellationToken).ConfigureAwait(false);
#endif
            if (read <= 0) {
                break;
            }

            var chunk = new byte[read];
            Buffer.BlockCopy(buffer, 0, chunk, 0, read);
            var start = offset;
            var end = offset + read - 1;
            await graph.UploadAttachmentChunkAsync(
                session.UploadUrl!,
                chunk,
                start,
                end,
                attachment.Length,
                cancellationToken).ConfigureAwait(false);
            offset += read;
        }
    }

    private static GraphApiClient GetGraphApiClient(GraphMailboxBrowser browser) {
        var field = typeof(GraphMailboxBrowser).GetField("_graph", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(browser) is GraphApiClient graph) {
            return graph;
        }
        throw new InvalidOperationException("Unable to resolve Graph API client from mailbox browser.");
    }

    private static GmailApiClient GetGmailApiClient(GmailMailboxBrowser browser) {
        var field = typeof(GmailMailboxBrowser).GetField("_gmail", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(browser) is GmailApiClient gmail) {
            return gmail;
        }
        throw new InvalidOperationException("Unable to resolve Gmail API client from mailbox browser.");
    }

    private static string GetGmailUserId(GmailMailboxBrowser browser) {
        var field = typeof(GmailMailboxBrowser).GetField("_userId", BindingFlags.Instance | BindingFlags.NonPublic);
        var value = field?.GetValue(browser) as string;
        return string.IsNullOrWhiteSpace(value) ? "me" : value.Trim();
    }

    private static string ResolveGraphFolder(string? requestedFolder, string? configuredFolder, string fallback) {
        var folder = ResolveFolder(requestedFolder, configuredFolder, fallback);
        if (folder.Equals("sent", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("sent items", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("sentitems", StringComparison.OrdinalIgnoreCase)) {
            return "sentitems";
        }
        return folder;
    }

    private static string ResolveGmailFolder(string? requestedFolder, string? configuredFolder, string fallback) {
        var folder = ResolveFolder(requestedFolder, configuredFolder, fallback);
        if (folder.Equals("sent", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("sent mail", StringComparison.OrdinalIgnoreCase) ||
            folder.Equals("sent items", StringComparison.OrdinalIgnoreCase)) {
            return "SENT";
        }
        return folder;
    }

    private static string ResolveFolder(string? requestedFolder, string? configuredFolder, string fallback) {
        var requested = (requestedFolder ?? string.Empty).Trim();
        if (requested.Length > 0) {
            return requested;
        }
        var configured = (configuredFolder ?? string.Empty).Trim();
        if (configured.Length > 0) {
            return configured;
        }
        return fallback;
    }

    private static string EncodeMimeAsBase64Url(MimeMessage message) {
        using var ms = new MemoryStream();
        message.WriteTo(ms);
        return GraphMimePreparation.Base64UrlEncode(ms.ToArray());
    }

    private static string? NormalizeMessageIdToken(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        var trimmed = value.Trim();
        if (trimmed.StartsWith("<", StringComparison.Ordinal)) {
            trimmed = trimmed.Substring(1);
        }
        if (trimmed.EndsWith(">", StringComparison.Ordinal)) {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }
        trimmed = trimmed.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}

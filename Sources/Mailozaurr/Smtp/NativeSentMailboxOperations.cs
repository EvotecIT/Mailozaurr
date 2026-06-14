using MimeKit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Shared native-provider helpers for Sent-folder append and duplicate detection.
/// </summary>
public static class NativeSentMailboxOperations {
    /// <summary>
    /// Default Graph folder used for Sent copy operations.
    /// </summary>
    public const string DefaultGraphSentFolder = "Sent Items";

    /// <summary>
    /// Default Gmail system Sent label id.
    /// </summary>
    public const string DefaultGmailSentLabelId = "SENT";

    /// <summary>
    /// Result of native Sent append operation.
    /// </summary>
    public sealed class NativeSentAppendResult {
        /// <summary>True when append succeeded.</summary>
        public bool Appended { get; set; }

        /// <summary>Resolved folder/label display name.</summary>
        public string? Folder { get; set; }

        /// <summary>Resolved provider message id when available.</summary>
        public string? MessageId { get; set; }
    }

    /// <summary>
    /// Result of native Sent duplicate probe.
    /// </summary>
    public sealed class NativeSentDuplicateProbeResult {
        /// <summary>True when duplicate message was found.</summary>
        public bool IsMatch { get; set; }

        /// <summary>Resolved folder/label display name.</summary>
        public string? Folder { get; set; }

        /// <summary>Matched message-id token.</summary>
        public string? MessageId { get; set; }

        /// <summary>Non-match helper value.</summary>
        public static NativeSentDuplicateProbeResult None { get; } = new();
    }

    /// <summary>
    /// Resolves Graph Sent folder name using request override, account override, and fallback.
    /// </summary>
    public static string ResolveGraphSentFolderName(
        string? requestedSentFolder,
        string? configuredSentFolder,
        string fallbackFolder = DefaultGraphSentFolder) {
        return ResolveFolderName(requestedSentFolder, configuredSentFolder, fallbackFolder);
    }

    /// <summary>
    /// Resolves Graph Sent folder selector value accepted by Graph APIs.
    /// </summary>
    public static string ResolveGraphSentFolderSelector(
        string? requestedSentFolder,
        string? configuredSentFolder,
        string fallbackFolder = DefaultGraphSentFolder) {
        var folderName = ResolveGraphSentFolderName(requestedSentFolder, configuredSentFolder, fallbackFolder);
        return GraphMailboxBrowser.ResolveFolderSelector(folderName);
    }

    /// <summary>
    /// Resolves Gmail Sent label id for native Sent copy operations.
    /// </summary>
    public static string ResolveGmailSentLabelId(
        string? requestedSentFolder = null,
        string? configuredSentFolder = null) {
        _ = requestedSentFolder;
        _ = configuredSentFolder;
        return DefaultGmailSentLabelId;
    }

    /// <summary>
    /// Resolves Gmail Sent folder display value used for caller metadata.
    /// </summary>
    public static string ResolveGmailSentFolderName(
        string? requestedSentFolder,
        string? configuredSentFolder,
        string fallbackFolder = DefaultGmailSentLabelId) {
        return ResolveFolderName(requestedSentFolder, configuredSentFolder, fallbackFolder);
    }

    /// <summary>
    /// Imports a MIME message into Graph Sent folder.
    /// </summary>
    public static async Task<NativeSentAppendResult> AppendToGraphSentAsync(
        GraphMailboxBrowser browser,
        MimeMessage message,
        string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        int maxInlineAttachmentBytes = GraphMimePreparation.DefaultMaxInlineAttachmentBytes,
        string? idempotencyHeaderName = null,
        CancellationToken cancellationToken = default) {
        if (browser == null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        if (maxInlineAttachmentBytes < 0) {
            throw new ArgumentOutOfRangeException(nameof(maxInlineAttachmentBytes), "maxInlineAttachmentBytes must be zero or greater.");
        }

        var folder = ResolveGraphSentFolderName(requestedSentFolder, configuredSentFolder);
        var imported = await browser.ImportMessageAsync(
            message,
            folder: folder,
            maxInlineAttachmentBytes: maxInlineAttachmentBytes,
            idempotencyHeaderName: idempotencyHeaderName,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new NativeSentAppendResult {
            Appended = true,
            Folder = folder,
            MessageId = NormalizeOptional(imported.MessageId)
        };
    }

    /// <summary>
    /// Imports a MIME message into Gmail Sent label.
    /// </summary>
    public static async Task<NativeSentAppendResult> AppendToGmailSentAsync(
        GmailMailboxBrowser browser,
        MimeMessage message,
        string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        CancellationToken cancellationToken = default) {
        if (browser == null) {
            throw new ArgumentNullException(nameof(browser));
        }
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }

        var labelId = ResolveGmailSentLabelId(requestedSentFolder, configuredSentFolder);
        _ = await browser.ImportMessageAsync(
            message,
            labelId: labelId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new NativeSentAppendResult {
            Appended = true,
            Folder = ResolveGmailSentFolderName(requestedSentFolder, configuredSentFolder),
            MessageId = NormalizeMessageIdToken(message.MessageId)
        };
    }

    /// <summary>
    /// Probes Graph Sent folder for duplicate RFC822 message-id.
    /// </summary>
    public static async Task<NativeSentDuplicateProbeResult> FindGraphSentDuplicateAsync(
        GraphMailboxBrowser browser,
        string messageIdToken,
        string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        CancellationToken cancellationToken = default) {
        if (browser == null) {
            throw new ArgumentNullException(nameof(browser));
        }

        var normalizedToken = NormalizeMessageIdToken(messageIdToken);
        if (normalizedToken == null) {
            throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        }

        var folder = ResolveGraphSentFolderName(requestedSentFolder, configuredSentFolder);
        var probe = await browser.FindMessageByInternetMessageIdAsync(
            normalizedToken,
            folder: folder,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!probe.IsMatch) {
            return NativeSentDuplicateProbeResult.None;
        }

        return new NativeSentDuplicateProbeResult {
            IsMatch = true,
            Folder = folder,
            MessageId = NormalizeMessageIdToken(probe.MessageId) ?? normalizedToken
        };
    }

    /// <summary>
    /// Probes Gmail Sent label for duplicate RFC822 message-id.
    /// </summary>
    public static async Task<NativeSentDuplicateProbeResult> FindGmailSentDuplicateAsync(
        GmailMailboxBrowser browser,
        string messageIdToken,
        string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        CancellationToken cancellationToken = default) {
        if (browser == null) {
            throw new ArgumentNullException(nameof(browser));
        }

        var normalizedToken = NormalizeMessageIdToken(messageIdToken);
        if (normalizedToken == null) {
            throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        }

        var labelId = ResolveGmailSentLabelId(requestedSentFolder, configuredSentFolder);
        var probe = await browser.FindSentMessageByRfc822MessageIdAsync(
            normalizedToken,
            sentLabelId: labelId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!probe.IsMatch) {
            return NativeSentDuplicateProbeResult.None;
        }

        return new NativeSentDuplicateProbeResult {
            IsMatch = true,
            Folder = ResolveGmailSentFolderName(requestedSentFolder, configuredSentFolder),
            MessageId = NormalizeMessageIdToken(probe.MessageId) ?? normalizedToken
        };
    }

    private static string ResolveFolderName(string? requestedFolder, string? configuredFolder, string fallbackFolder) {
        var requested = NormalizeOptional(requestedFolder);
        if (requested != null) {
            return requested;
        }

        var configured = NormalizeOptional(configuredFolder);
        if (configured != null) {
            return configured;
        }

        var fallback = NormalizeOptional(fallbackFolder);
        return fallback ?? DefaultGraphSentFolder;
    }

    private static string? NormalizeOptional(string? value) {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeMessageIdToken(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var token = value!.Trim();
        if (token.StartsWith("<", StringComparison.Ordinal)) {
            token = token.Substring(1);
        }
        if (token.EndsWith(">", StringComparison.Ordinal)) {
            token = token.Substring(0, token.Length - 1);
        }

        token = token.Trim();
        return token.Length == 0 ? null : token;
    }
}
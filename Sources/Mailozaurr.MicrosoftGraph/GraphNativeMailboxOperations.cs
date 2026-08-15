using MimeKit;

namespace Mailozaurr;

/// <summary>Microsoft Graph threading metadata and native Sent-folder operations.</summary>
public static class GraphNativeMailboxOperations {
    /// <summary>Default Graph Sent folder.</summary>
    public const string DefaultSentFolder = "Sent Items";

    /// <summary>Reads and normalizes Graph threading metadata.</summary>
    public static async Task<NativeMailboxThreadingMetadataOperations.NativeMailboxThreadingMetadataResult>
        GetThreadingMetadataAsync(GraphMailboxBrowser browser, string nativeId,
            int maxMimeBytes = GraphMailboxBrowser.DefaultThreadingMetadataMaxMimeBytes,
            CancellationToken cancellationToken = default) {
        if (browser == null) throw new ArgumentNullException(nameof(browser));
        if (string.IsNullOrWhiteSpace(nativeId)) throw new ArgumentException("nativeId is required.", nameof(nativeId));
        GraphMailboxBrowser.GraphMailboxThreadingMetadataResult metadata =
            await browser.GetThreadingMetadataAsync(nativeId.Trim(), maxMimeBytes: maxMimeBytes,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        return NativeMailboxThreadingMetadataOperations.Normalize(metadata.MessageId, metadata.ReplyTo,
            metadata.Cc, metadata.InReplyTo, metadata.References);
    }

    /// <summary>Resolves the Graph Sent folder name.</summary>
    public static string ResolveSentFolderName(string? requestedSentFolder, string? configuredSentFolder,
        string fallbackFolder = DefaultSentFolder) => NativeSentMailboxOperations.ResolveFolderName(
            requestedSentFolder, configuredSentFolder, fallbackFolder);

    /// <summary>Resolves the Graph folder selector accepted by Graph APIs.</summary>
    public static string ResolveSentFolderSelector(string? requestedSentFolder, string? configuredSentFolder,
        string fallbackFolder = DefaultSentFolder) => GraphMailboxBrowser.ResolveFolderSelector(
            ResolveSentFolderName(requestedSentFolder, configuredSentFolder, fallbackFolder));

    /// <summary>Imports a MIME message into the Graph Sent folder.</summary>
    public static async Task<NativeSentMailboxOperations.NativeSentAppendResult> AppendToSentAsync(
        GraphMailboxBrowser browser, MimeMessage message, string? requestedSentFolder = null,
        string? configuredSentFolder = null,
        int maxInlineAttachmentBytes = GraphMimePreparation.DefaultMaxInlineAttachmentBytes,
        string? idempotencyHeaderName = null, CancellationToken cancellationToken = default) {
        if (browser == null) throw new ArgumentNullException(nameof(browser));
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (maxInlineAttachmentBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxInlineAttachmentBytes));
        string folder = ResolveSentFolderName(requestedSentFolder, configuredSentFolder);
        GraphMailboxBrowser.GraphMailboxImportResult imported = await browser.ImportMessageAsync(message, folder: folder,
            maxInlineAttachmentBytes: maxInlineAttachmentBytes,
            idempotencyHeaderName: idempotencyHeaderName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new NativeSentMailboxOperations.NativeSentAppendResult {
            Appended = true, Folder = folder,
            MessageId = NativeSentMailboxOperations.NormalizeMessageIdToken(imported.MessageId)
        };
    }

    /// <summary>Probes the Graph Sent folder for a duplicate RFC822 Message-Id.</summary>
    public static async Task<NativeSentMailboxOperations.NativeSentDuplicateProbeResult> FindSentDuplicateAsync(
        GraphMailboxBrowser browser, string messageIdToken, string? requestedSentFolder = null,
        string? configuredSentFolder = null, CancellationToken cancellationToken = default) {
        if (browser == null) throw new ArgumentNullException(nameof(browser));
        string normalized = NativeSentMailboxOperations.NormalizeMessageIdToken(messageIdToken)
            ?? throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        string folder = ResolveSentFolderName(requestedSentFolder, configuredSentFolder);
        GraphMailboxBrowser.GraphMailboxDuplicateProbeResult probe =
            await browser.FindMessageByInternetMessageIdAsync(normalized, folder: folder,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        return !probe.IsMatch ? NativeSentMailboxOperations.NativeSentDuplicateProbeResult.None
            : new NativeSentMailboxOperations.NativeSentDuplicateProbeResult {
                IsMatch = true, Folder = folder,
                MessageId = NativeSentMailboxOperations.NormalizeMessageIdToken(probe.MessageId) ?? normalized
            };
    }
}

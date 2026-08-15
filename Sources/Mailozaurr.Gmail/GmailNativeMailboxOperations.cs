using MimeKit;

namespace Mailozaurr;

/// <summary>Gmail threading metadata and native Sent-label operations.</summary>
public static class GmailNativeMailboxOperations {
    /// <summary>Default Gmail Sent system label id.</summary>
    public const string DefaultSentLabelId = "SENT";

    /// <summary>Reads and normalizes Gmail threading metadata.</summary>
    public static async Task<NativeMailboxThreadingMetadataOperations.NativeMailboxThreadingMetadataResult>
        GetThreadingMetadataAsync(GmailMailboxBrowser browser, string nativeId,
            CancellationToken cancellationToken = default) {
        if (browser == null) throw new ArgumentNullException(nameof(browser));
        if (string.IsNullOrWhiteSpace(nativeId)) throw new ArgumentException("nativeId is required.", nameof(nativeId));
        GmailMailboxBrowser.GmailMailboxThreadingMetadataResult metadata =
            await browser.GetThreadingMetadataAsync(nativeId.Trim(), cancellationToken).ConfigureAwait(false);
        return NativeMailboxThreadingMetadataOperations.Normalize(metadata.MessageId, metadata.ReplyTo,
            metadata.Cc, metadata.InReplyTo, metadata.References);
    }

    /// <summary>Resolves the Gmail Sent system label id.</summary>
    public static string ResolveSentLabelId(string? requestedSentFolder = null,
        string? configuredSentFolder = null) {
        _ = requestedSentFolder; _ = configuredSentFolder; return DefaultSentLabelId;
    }

    /// <summary>Resolves the Gmail Sent display name.</summary>
    public static string ResolveSentFolderName(string? requestedSentFolder, string? configuredSentFolder,
        string fallbackFolder = DefaultSentLabelId) => NativeSentMailboxOperations.ResolveFolderName(
            requestedSentFolder, configuredSentFolder, fallbackFolder);

    /// <summary>Imports a MIME message into Gmail Sent.</summary>
    public static async Task<NativeSentMailboxOperations.NativeSentAppendResult> AppendToSentAsync(
        GmailMailboxBrowser browser, MimeMessage message, string? requestedSentFolder = null,
        string? configuredSentFolder = null, CancellationToken cancellationToken = default) {
        if (browser == null) throw new ArgumentNullException(nameof(browser));
        if (message == null) throw new ArgumentNullException(nameof(message));
        string labelId = ResolveSentLabelId(requestedSentFolder, configuredSentFolder);
        _ = await browser.ImportMessageAsync(message, labelId: labelId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new NativeSentMailboxOperations.NativeSentAppendResult {
            Appended = true,
            Folder = ResolveSentFolderName(requestedSentFolder, configuredSentFolder),
            MessageId = NativeSentMailboxOperations.NormalizeMessageIdToken(message.MessageId)
        };
    }

    /// <summary>Probes Gmail Sent for a duplicate RFC822 Message-Id.</summary>
    public static async Task<NativeSentMailboxOperations.NativeSentDuplicateProbeResult> FindSentDuplicateAsync(
        GmailMailboxBrowser browser, string messageIdToken, string? requestedSentFolder = null,
        string? configuredSentFolder = null, CancellationToken cancellationToken = default) {
        if (browser == null) throw new ArgumentNullException(nameof(browser));
        string normalized = NativeSentMailboxOperations.NormalizeMessageIdToken(messageIdToken)
            ?? throw new ArgumentException("messageIdToken is required.", nameof(messageIdToken));
        string labelId = ResolveSentLabelId(requestedSentFolder, configuredSentFolder);
        GmailMailboxBrowser.GmailMailboxDuplicateProbeResult probe =
            await browser.FindSentMessageByRfc822MessageIdAsync(normalized, sentLabelId: labelId,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        return !probe.IsMatch ? NativeSentMailboxOperations.NativeSentDuplicateProbeResult.None
            : new NativeSentMailboxOperations.NativeSentDuplicateProbeResult {
                IsMatch = true,
                Folder = ResolveSentFolderName(requestedSentFolder, configuredSentFolder),
                MessageId = NativeSentMailboxOperations.NormalizeMessageIdToken(probe.MessageId) ?? normalized
            };
    }
}

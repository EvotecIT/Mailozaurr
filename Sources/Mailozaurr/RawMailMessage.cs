namespace Mailozaurr;

/// <summary>Bounded provider-supplied RFC 822 content for one mailbox message.</summary>
public sealed class RawMailMessage : IDisposable {
    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Unmodified RFC 822 bytes returned by the provider.</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>Optional provider stream used by bounded export without materializing another byte array.</summary>
    public Stream? ContentStream { get; set; }

    /// <summary>
    /// Optional provider epoch or namespace component required to keep persisted identities stable.
    /// </summary>
    /// <remarks>
    /// IMAP sources use this for UIDVALIDITY because a numeric UID is reusable after the mailbox
    /// epoch changes. Other providers normally leave it unset.
    /// </remarks>
    public string? StorageIdentityComponent { get; set; }

    /// <summary>Closes provider content when this result owns a stream.</summary>
    public void Dispose() => ContentStream?.Dispose();
}

/// <summary>Request for provider-native RFC 822 message content.</summary>
public sealed class RawMailMessageRequest {
    /// <summary>Optional mailbox identifier for multi-mailbox providers.</summary>
    public string? MailboxId { get; set; }

    /// <summary>Optional folder identifier or path.</summary>
    public string? FolderId { get; set; }

    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Maximum accepted provider payload size.</summary>
    public long MaxBytes { get; set; }
}

/// <summary>Reusable provider session for bounded RFC 822 message retrieval.</summary>
public interface IRawMailMessageSession : IDisposable {
    /// <summary>Gets one bounded provider message through the open session.</summary>
    Task<RawMailMessage?> GetRawMessageAsync(
        RawMailMessageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional raw-message session that can transfer a provider stream to the exporter.</summary>
public interface IStreamingRawMailMessageSession {
    /// <summary>Opens one bounded provider message. The caller disposes the returned result.</summary>
    Task<RawMailMessage?> GetRawMessageStreamAsync(
        RawMailMessageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Exposes the provider's current identity namespace for a folder.</summary>
public interface IRawMailMessageScopeSession {
    /// <summary>Returns a stable scope token, such as the IMAP folder's UIDVALIDITY.</summary>
    Task<string> GetScopeAsync(string? mailboxId, string? folderId,
        CancellationToken cancellationToken = default);
}

/// <summary>Provider-specific source of batch-scoped raw RFC 822 sessions.</summary>
public interface IRawMailMessageSource {
    /// <summary>Profile kind handled by this source.</summary>
    MailProfileKind Kind { get; }

    /// <summary>Opens one reusable provider session for a bounded export batch.</summary>
    Task<IRawMailMessageSession> OpenSessionAsync(
        MailProfile profile,
        CancellationToken cancellationToken = default);
}

internal interface IArchiveRawMailMessageSource {
    Task<IRawMailMessageSession> OpenArchiveSessionAsync(
        MailProfile profile, CancellationToken cancellationToken);
}

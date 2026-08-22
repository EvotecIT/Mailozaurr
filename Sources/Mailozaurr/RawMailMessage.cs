namespace Mailozaurr;

/// <summary>Bounded provider-supplied RFC 822 content for one mailbox message.</summary>
public sealed class RawMailMessage {
    /// <summary>Provider-specific message identifier.</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Unmodified RFC 822 bytes returned by the provider.</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();
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

/// <summary>Provider-specific source of raw RFC 822 message content.</summary>
public interface IRawMailMessageSource {
    /// <summary>Profile kind handled by this source.</summary>
    MailProfileKind Kind { get; }

    /// <summary>Gets one bounded provider message.</summary>
    Task<RawMailMessage?> GetRawMessageAsync(
        MailProfile profile,
        RawMailMessageRequest request,
        CancellationToken cancellationToken = default);
}

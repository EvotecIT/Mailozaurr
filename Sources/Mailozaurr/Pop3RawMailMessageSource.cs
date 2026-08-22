namespace Mailozaurr;

/// <summary>Retrieves unmodified RFC 822 content from a POP3 mailbox.</summary>
public sealed class Pop3RawMailMessageSource : IRawMailMessageSource {
    private readonly IPop3SessionFactory _sessionFactory;

    /// <summary>Creates a POP3 raw-message source.</summary>
    public Pop3RawMailMessageSource(IPop3SessionFactory sessionFactory) =>
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Pop3;

    /// <inheritdoc />
    public async Task<RawMailMessage?> GetRawMessageAsync(
        MailProfile profile,
        RawMailMessageRequest request,
        CancellationToken cancellationToken = default) {
        _ = Pop3MailReadHandler.NormalizeFolderId(request.FolderId);
        using var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        var identifier = Pop3MailReadHandler.ParseMessageId(request.MessageId);
        if (!string.IsNullOrWhiteSpace(identifier.Uid)) {
            var uids = await client.GetMessageUidsAsync(cancellationToken).ConfigureAwait(false);
            var index = -1;
            for (var candidate = 0; candidate < uids.Count; candidate++) {
                if (string.Equals(uids[candidate], identifier.Uid, StringComparison.Ordinal)) {
                    index = candidate;
                    break;
                }
            }
            return index < 0
                ? null
                : await ReadAtIndexAsync(client, request, index, cancellationToken).ConfigureAwait(false);
        }

        var occurrence = 0;
        for (var index = client.Count - 1; index >= 0; index--) {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = await ReadAtIndexAsync(client, request, index, cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(candidate.Content, writable: false);
            var message = MimeKit.MimeMessage.Load(stream, cancellationToken);
            if (!string.Equals(
                    Pop3MailReadHandler.ComputeMessageFingerprint(message),
                    identifier.Fingerprint,
                    StringComparison.Ordinal)) {
                continue;
            }
            if (occurrence++ == identifier.Occurrence) return candidate;
        }

        return null;
    }

    private static async Task<RawMailMessage> ReadAtIndexAsync(
        MailKit.Net.Pop3.Pop3Client client,
        RawMailMessageRequest request,
        int index,
        CancellationToken cancellationToken) {
        var announcedSize = client.GetMessageSize(index, cancellationToken);
        if (announcedSize > request.MaxBytes) {
            throw new InvalidDataException($"POP3 MIME content exceeds {request.MaxBytes} bytes.");
        }

        using var stream = await client.GetStreamAsync(
            index,
            headersOnly: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new RawMailMessage {
            MessageId = request.MessageId,
            Content = await RawMailMessageSourceUtilities.ReadBoundedAsync(stream, request.MaxBytes, cancellationToken)
                .ConfigureAwait(false)
        };
    }
}

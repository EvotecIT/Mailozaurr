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
        var oversizedHeaderFingerprints = new HashSet<string>(StringComparer.Ordinal);
        for (var index = client.Count - 1; index >= 0; index--) {
            cancellationToken.ThrowIfCancellationRequested();
            var announcedSize = client.GetMessageSize(index, cancellationToken);
            if (announcedSize > request.MaxBytes) {
                var headers = await client.GetMessageHeadersAsync(index, cancellationToken).ConfigureAwait(false);
                oversizedHeaderFingerprints.Add(ComputeHeaderFingerprint(headers));
                continue;
            }
            var candidate = await ReadAtIndexAsync(client, request, index, cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(candidate.Content, writable: false);
            var message = MimeKit.MimeMessage.Load(stream, cancellationToken);
            if (!string.Equals(
                    Pop3MailReadHandler.ComputeMessageFingerprint(message),
                    identifier.Fingerprint,
                    StringComparison.Ordinal)) {
                continue;
            }
            if (oversizedHeaderFingerprints.Contains(ComputeHeaderFingerprint(message.Headers))) {
                throw new InvalidDataException(
                    "POP3 hash identity is ambiguous because an oversized earlier message has the same headers.");
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

    internal static string ComputeHeaderFingerprint(IEnumerable<MimeKit.Header> headers) {
        using var algorithm = System.Security.Cryptography.SHA256.Create();
        using var sink = new System.Security.Cryptography.CryptoStream(
            Stream.Null,
            algorithm,
            System.Security.Cryptography.CryptoStreamMode.Write);
        foreach (var header in headers) {
            var field = System.Text.Encoding.UTF8.GetBytes(header.Field ?? string.Empty);
            var value = System.Text.Encoding.UTF8.GetBytes(header.Value ?? string.Empty);
            WriteLengthPrefixed(sink, field);
            WriteLengthPrefixed(sink, value);
        }
        sink.FlushFinalBlock();
        return Convert.ToBase64String(algorithm.Hash!)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static void WriteLengthPrefixed(Stream destination, byte[] value) {
        var length = BitConverter.GetBytes(value.Length);
        destination.Write(length, 0, length.Length);
        destination.Write(value, 0, value.Length);
    }
}

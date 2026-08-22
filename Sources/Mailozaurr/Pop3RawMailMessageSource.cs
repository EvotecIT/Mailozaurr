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
    public async Task<IRawMailMessageSession> OpenSessionAsync(
        MailProfile profile,
        CancellationToken cancellationToken = default) {
        var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return new Session(client);
    }

    private sealed class Session : IRawMailMessageSession {
        private readonly MailKit.Net.Pop3.Pop3Client _client;
        private readonly Dictionary<long, Dictionary<string, List<int>>> _fingerprintIndexes = new();
        private IList<string>? _uids;

        internal Session(MailKit.Net.Pop3.Pop3Client client) => _client = client;

        public async Task<RawMailMessage?> GetRawMessageAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            _ = Pop3MailReadHandler.NormalizeFolderId(request.FolderId);
            var identifier = Pop3MailReadHandler.ParseMessageId(request.MessageId);
            if (!string.IsNullOrWhiteSpace(identifier.Uid)) {
                var uids = _uids ??= await _client.GetMessageUidsAsync(cancellationToken).ConfigureAwait(false);
                var index = -1;
                for (var candidate = 0; candidate < uids.Count; candidate++) {
                    if (string.Equals(uids[candidate], identifier.Uid, StringComparison.Ordinal)) {
                        index = candidate;
                        break;
                    }
                }
                return index < 0
                    ? null
                    : await ReadAtIndexAsync(_client, request, index, cancellationToken).ConfigureAwait(false);
            }

            if (!_fingerprintIndexes.TryGetValue(request.MaxBytes, out var fingerprintIndex)) {
                var indexedMatch = await BuildFingerprintIndexAsync(
                        request.MaxBytes,
                        identifier.Fingerprint!,
                        identifier.Occurrence,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (indexedMatch != null) {
                    indexedMatch.MessageId = request.MessageId;
                    return indexedMatch;
                }
                fingerprintIndex = _fingerprintIndexes[request.MaxBytes];
            }
            if (!fingerprintIndex.TryGetValue(identifier.Fingerprint!, out var matchingIndexes) ||
                identifier.Occurrence >= matchingIndexes.Count) {
                return null;
            }

            return await ReadAtIndexAsync(
                    _client,
                    request,
                    matchingIndexes[identifier.Occurrence],
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public void Dispose() => _client.Dispose();

        private async Task<RawMailMessage?> BuildFingerprintIndexAsync(
            long maxBytes,
            string requestedFingerprint,
            int requestedOccurrence,
            CancellationToken cancellationToken) {
            var indexByFingerprint = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            RawMailMessage? requestedMessage = null;
            for (var index = _client.Count - 1; index >= 0; index--) {
                cancellationToken.ThrowIfCancellationRequested();
                var announcedSize = _client.GetMessageSize(index, cancellationToken);
                if (announcedSize > maxBytes) {
                    continue;
                }
                var candidate = await ReadAtIndexAsync(
                        _client,
                        new RawMailMessageRequest { MessageId = string.Empty, MaxBytes = maxBytes },
                        index,
                        cancellationToken)
                    .ConfigureAwait(false);
                using var stream = new MemoryStream(candidate.Content, writable: false);
                var message = MimeKit.MimeMessage.Load(stream, cancellationToken);
                var fingerprint = Pop3MailReadHandler.ComputeMessageFingerprint(message);
                if (!indexByFingerprint.TryGetValue(fingerprint, out var indexes)) {
                    indexes = new List<int>();
                    indexByFingerprint.Add(fingerprint, indexes);
                }
                if (requestedMessage == null &&
                    string.Equals(fingerprint, requestedFingerprint, StringComparison.Ordinal) &&
                    indexes.Count == requestedOccurrence) {
                    requestedMessage = candidate;
                }
                indexes.Add(index);
            }

            _fingerprintIndexes.Add(maxBytes, indexByFingerprint);
            return requestedMessage;
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
}

using MailKit;

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

    private sealed class Session : IRawMailMessageSession, IStreamingRawMailMessageSession {
        private readonly MailKit.Net.Pop3.Pop3Client _client;
        private readonly Dictionary<long, Dictionary<string, List<int>>> _fingerprintIndexes = new();
        private readonly Dictionary<long, bool> _fingerprintIndexesSkippedOversized = new();
        private IList<string>? _uids;
        private Dictionary<string, int>? _uidIndexes;

        internal Session(MailKit.Net.Pop3.Pop3Client client) => _client = client;

        public async Task<RawMailMessage?> GetRawMessageAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            _ = Pop3MailReadHandler.NormalizeFolderId(request.FolderId);
            var identifier = Pop3MailReadHandler.ParseMessageId(request.MessageId);
            if (!string.IsNullOrWhiteSpace(identifier.Uid)) {
                var indexes = await GetUidIndexesAsync(cancellationToken).ConfigureAwait(false);
                return !indexes.TryGetValue(identifier.Uid!, out var index)
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
                if (_fingerprintIndexesSkippedOversized[request.MaxBytes]) {
                    throw new InvalidDataException(
                        $"POP3 hash identity cannot be resolved without reading messages larger than {request.MaxBytes} bytes.");
                }
                return null;
            }

            return await ReadAtIndexAsync(
                    _client,
                    request,
                    matchingIndexes[identifier.Occurrence],
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<RawMailMessage?> GetRawMessageStreamAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            _ = Pop3MailReadHandler.NormalizeFolderId(request.FolderId);
            var identifier = Pop3MailReadHandler.ParseMessageId(request.MessageId);
            if (string.IsNullOrWhiteSpace(identifier.Uid)) {
                // Hash identities require mailbox-wide fingerprint resolution.
                // Retain that existing bounded path and stream its selected result.
                var buffered = await GetRawMessageAsync(request, cancellationToken).ConfigureAwait(false);
                if (buffered == null) return null;
                return new RawMailMessage {
                    MessageId = buffered.MessageId,
                    StorageIdentityComponent = buffered.StorageIdentityComponent,
                    ContentStream = new MemoryStream(buffered.Content, writable: false)
                };
            }

            var indexes = await GetUidIndexesAsync(cancellationToken).ConfigureAwait(false);
            if (indexes.TryGetValue(identifier.Uid!, out var index)) {
                var announcedSize = _client.GetMessageSize(index, cancellationToken);
                if (announcedSize > request.MaxBytes) {
                    throw new InvalidDataException($"POP3 MIME content exceeds {request.MaxBytes} bytes.");
                }
                var stream = await _client.GetStreamAsync(index, headersOnly: false,
                    cancellationToken: cancellationToken,
                    progress: new BoundedTransferProgress(request.MaxBytes)).ConfigureAwait(false);
                return new RawMailMessage { MessageId = request.MessageId, ContentStream = stream };
            }
            return null;
        }

        private async Task<Dictionary<string, int>> GetUidIndexesAsync(CancellationToken cancellationToken) {
            if (_uidIndexes != null) return _uidIndexes;
            var uids = _uids ??= await _client.GetMessageUidsAsync(cancellationToken).ConfigureAwait(false);
            var indexes = new Dictionary<string, int>(uids.Count, StringComparer.Ordinal);
            for (var index = 0; index < uids.Count; index++) {
                if (!indexes.ContainsKey(uids[index])) indexes.Add(uids[index], index);
            }
            return _uidIndexes = indexes;
        }

        public void Dispose() => _client.Dispose();

        private async Task<RawMailMessage?> BuildFingerprintIndexAsync(
            long maxBytes,
            string requestedFingerprint,
            int requestedOccurrence,
            CancellationToken cancellationToken) {
            var indexByFingerprint = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            RawMailMessage? requestedMessage = null;
            var skippedOversized = false;
            for (var index = _client.Count - 1; index >= 0; index--) {
                cancellationToken.ThrowIfCancellationRequested();
                var announcedSize = _client.GetMessageSize(index, cancellationToken);
                if (announcedSize > maxBytes) {
                    skippedOversized = true;
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
            _fingerprintIndexesSkippedOversized.Add(maxBytes, skippedOversized);
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
                cancellationToken: cancellationToken,
                progress: new BoundedTransferProgress(request.MaxBytes)).ConfigureAwait(false);
            return new RawMailMessage {
                MessageId = request.MessageId,
                Content = await RawMailMessageSourceUtilities.ReadBoundedAsync(stream, request.MaxBytes, cancellationToken)
                    .ConfigureAwait(false)
            };
        }
    }

    private sealed class BoundedTransferProgress : ITransferProgress {
        private readonly long _maxBytes;

        internal BoundedTransferProgress(long maxBytes) => _maxBytes = maxBytes;

        public void Report(long bytesTransferred) {
            if (bytesTransferred > _maxBytes)
                throw new InvalidDataException($"POP3 MIME content exceeds {_maxBytes} bytes.");
        }

        public void Report(long bytesTransferred, long totalSize) => Report(bytesTransferred);
    }
}

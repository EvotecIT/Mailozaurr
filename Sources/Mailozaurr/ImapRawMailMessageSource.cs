using MailKit;

namespace Mailozaurr;

/// <summary>Retrieves unmodified RFC 822 content from an IMAP mailbox.</summary>
public sealed class ImapRawMailMessageSource : IRawMailMessageSource, IArchiveRawMailMessageSource {
    private readonly IImapSessionFactory _sessionFactory;

    /// <summary>Creates an IMAP raw-message source.</summary>
    public ImapRawMailMessageSource(IImapSessionFactory sessionFactory) =>
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Imap;

    /// <inheritdoc />
    public async Task<IRawMailMessageSession> OpenSessionAsync(
        MailProfile profile,
        CancellationToken cancellationToken = default) {
        var client = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return new Session(profile, client);
    }

    async Task<IRawMailMessageSession> IArchiveRawMailMessageSource.OpenArchiveSessionAsync(
        MailProfile profile, CancellationToken cancellationToken) {
        if (_sessionFactory is ImapSessionFactory credentialFactory) {
            var authenticated = await credentialFactory.ConnectForArchiveAsync(profile, cancellationToken)
                .ConfigureAwait(false);
            return new Session(profile, authenticated.Client, authenticated.Scope);
        }
        throw new NotSupportedException(
            "IMAP archive resume requires a session factory that binds the authenticated credential.");
    }

    private sealed class Session : IRawMailMessageSession, IStreamingRawMailMessageSession, IRawMailMessageScopeSession {
        private readonly MailProfile _profile;
        private readonly MailKit.Net.Imap.ImapClient _client;
        private readonly string? _archiveScope;

        internal Session(MailProfile profile, MailKit.Net.Imap.ImapClient client, string? archiveScope = null) {
            _profile = profile;
            _client = client;
            _archiveScope = archiveScope;
        }

        public Task<string> GetScopeAsync(string? mailboxId, string? folderId,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = ImapMailReadHandler.ResolveFolder(folderId, _profile);
            var mailFolder = _client.GetCachedFolder(folder, FolderAccess.ReadOnly);
            var accountScope = _archiveScope ?? throw new NotSupportedException(
                "IMAP archive resume requires a session factory that binds the authenticated credential.");
            return Task.FromResult(accountScope + ":" +
                ImapMailReadHandler.CanonicalizeFolderForStorage(mailFolder.FullName) +
                ":uidvalidity:" + mailFolder.UidValidity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public async Task<RawMailMessage?> GetRawMessageAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            using var streamed = await GetRawMessageStreamAsync(request, cancellationToken).ConfigureAwait(false);
            if (streamed == null) return null;
            return new RawMailMessage {
                MessageId = streamed.MessageId,
                StorageIdentityComponent = streamed.StorageIdentityComponent,
                Content = await RawMailMessageSourceUtilities.ReadBoundedAsync(
                    streamed.ContentStream!, request.MaxBytes, cancellationToken).ConfigureAwait(false)
            };
        }

        public async Task<RawMailMessage?> GetRawMessageStreamAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            var folder = ImapMailReadHandler.ResolveFolder(request.FolderId, _profile);
            var uid = ImapMailReadHandler.ParseUid(request.MessageId);
            var mailFolder = _client.GetCachedFolder(folder, FolderAccess.ReadOnly);
            var summaries = await mailFolder.FetchAsync(
                new[] { uid },
                MessageSummaryItems.UniqueId | MessageSummaryItems.Size,
                cancellationToken).ConfigureAwait(false);
            var summary = summaries.FirstOrDefault(value => value.UniqueId == uid);
            if (summary == null) return null;
            if (summary.Size.HasValue && summary.Size.Value > request.MaxBytes) {
                throw new InvalidDataException($"IMAP MIME content exceeds {request.MaxBytes} bytes.");
            }
            var stream = new ChunkedImapMessageStream(mailFolder, uid, request.MaxBytes);
            return new RawMailMessage {
                MessageId = request.MessageId,
                StorageIdentityComponent = "uidvalidity:" + mailFolder.UidValidity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ContentStream = stream
            };
        }

        public void Dispose() => _client.Dispose();
    }

    // MailKit materializes one FETCH response in memory. Partial FETCH keeps each response bounded.
    private sealed class ChunkedImapMessageStream : Stream {
        private const int ChunkBytes = 1024 * 1024;
        private readonly IMailFolder _folder;
        private readonly UniqueId _uid;
        private readonly long _limit;
        private Stream? _current;
        private int _requested;
        private int _readFromCurrent;
        private long _position;
        private bool _finished;

        internal ChunkedImapMessageStream(IMailFolder folder, UniqueId uid, long maxBytes) {
            if (maxBytes >= int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maxBytes),
                    "IMAP content limit must permit one extra byte to detect oversized content.");
            _folder = folder;
            _uid = uid;
            _limit = maxBytes + 1;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException();
            if (count == 0 || _finished) return 0;
            while (true) {
                if (_current == null) {
                    if (_position >= _limit) {
                        _finished = true;
                        return 0;
                    }
                    _requested = (int)Math.Min(ChunkBytes, _limit - _position);
                    _readFromCurrent = 0;
                    _current = await _folder.GetStreamAsync(_uid, checked((int)_position), _requested,
                        cancellationToken).ConfigureAwait(false);
                }
                var read = await _current.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (read > 0) {
                    _position += read;
                    _readFromCurrent += read;
                    return read;
                }
                _current.Dispose();
                _current = null;
                if (_readFromCurrent < _requested) {
                    _finished = true;
                    return 0;
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) {
            if (disposing) _current?.Dispose();
            base.Dispose(disposing);
        }
    }
}

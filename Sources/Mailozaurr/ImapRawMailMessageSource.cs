using MailKit;

namespace Mailozaurr;

/// <summary>Retrieves unmodified RFC 822 content from an IMAP mailbox.</summary>
public sealed class ImapRawMailMessageSource : IRawMailMessageSource {
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

    private sealed class Session : IRawMailMessageSession {
        private readonly MailProfile _profile;
        private readonly MailKit.Net.Imap.ImapClient _client;

        internal Session(MailProfile profile, MailKit.Net.Imap.ImapClient client) {
            _profile = profile;
            _client = client;
        }

        public async Task<RawMailMessage?> GetRawMessageAsync(
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
            var requestedBytes = request.MaxBytes < int.MaxValue
                ? checked((int)request.MaxBytes + 1)
                : int.MaxValue;
            using var stream = await mailFolder.GetStreamAsync(
                uid,
                offset: 0,
                count: requestedBytes,
                cancellationToken).ConfigureAwait(false);
            return new RawMailMessage {
                MessageId = request.MessageId,
                StorageIdentityComponent = "uidvalidity:" + mailFolder.UidValidity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Content = await RawMailMessageSourceUtilities.ReadBoundedAsync(stream, request.MaxBytes, cancellationToken)
                    .ConfigureAwait(false)
            };
        }

        public void Dispose() => _client.Dispose();
    }
}

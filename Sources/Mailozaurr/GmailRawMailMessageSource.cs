namespace Mailozaurr;

/// <summary>Retrieves unmodified RFC 822 content from Gmail.</summary>
public sealed class GmailRawMailMessageSource : IRawMailMessageSource {
    private const string RawFields = "id,raw";
    private readonly IGmailSessionFactory _sessionFactory;

    /// <summary>Creates a Gmail raw-message source.</summary>
    public GmailRawMailMessageSource(IGmailSessionFactory sessionFactory) =>
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Gmail;

    /// <inheritdoc />
    public async Task<IRawMailMessageSession> OpenSessionAsync(
        MailProfile profile,
        CancellationToken cancellationToken = default) {
        var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return new Session(profile, session);
    }

    private sealed class Session : IRawMailMessageSession {
        private readonly MailProfile _profile;
        private readonly GmailSession _session;

        internal Session(MailProfile profile, GmailSession session) {
            _profile = profile;
            _session = session;
        }

        public async Task<RawMailMessage?> GetRawMessageAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            var userId = GmailMailReadHandler.ResolveUserId(_profile, request.MailboxId);
            GmailMessage message;
            try {
                message = await _session.Client.GetRawBoundedAsync(
                    userId,
                    request.MessageId,
                    request.MaxBytes,
                    fields: RawFields,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (GmailApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) {
                return null;
            }
            return new RawMailMessage {
                MessageId = request.MessageId,
                Content = RawMailMessageSourceUtilities.DecodeBase64Url(message.Raw ?? string.Empty, request.MaxBytes)
            };
        }

        public void Dispose() => _session.Dispose();
    }
}

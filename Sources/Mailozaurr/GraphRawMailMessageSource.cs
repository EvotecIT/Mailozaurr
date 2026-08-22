namespace Mailozaurr;

/// <summary>Retrieves unmodified RFC 822 content from Microsoft Graph.</summary>
public sealed class GraphRawMailMessageSource : IRawMailMessageSource {
    private readonly IGraphSessionFactory _sessionFactory;

    /// <summary>Creates a Graph raw-message source.</summary>
    public GraphRawMailMessageSource(IGraphSessionFactory sessionFactory) =>
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Graph;

    /// <inheritdoc />
    public async Task<IRawMailMessageSession> OpenSessionAsync(
        MailProfile profile,
        CancellationToken cancellationToken = default) {
        var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        return new Session(profile, session);
    }

    private sealed class Session : IRawMailMessageSession {
        private readonly MailProfile _profile;
        private readonly GraphSession _session;

        internal Session(MailProfile profile, GraphSession session) {
            _profile = profile;
            _session = session;
        }

        public async Task<RawMailMessage?> GetRawMessageAsync(
            RawMailMessageRequest request,
            CancellationToken cancellationToken = default) {
            var userId = GraphMailReadHandler.ResolveUserId(_profile, request.MailboxId);
            byte[] content;
            try {
                content = await _session.Client.GetMessageMimeAsync(
                    request.MessageId,
                    userId: userId,
                    maxBytes: checked((int)request.MaxBytes),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            } catch (GraphApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) {
                return null;
            }
            return new RawMailMessage { MessageId = request.MessageId, Content = content };
        }

        public void Dispose() => _session.Dispose();
    }
}

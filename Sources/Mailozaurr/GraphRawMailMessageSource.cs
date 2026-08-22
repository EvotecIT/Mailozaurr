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
    public async Task<RawMailMessage?> GetRawMessageAsync(
        MailProfile profile,
        RawMailMessageRequest request,
        CancellationToken cancellationToken = default) {
        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        var userId = GraphMailReadHandler.ResolveUserId(profile, request.MailboxId);
        byte[] content;
        try {
            content = await session.Client.GetMessageMimeAsync(
                request.MessageId,
                userId: userId,
                maxBytes: checked((int)request.MaxBytes),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        } catch (GraphApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) {
            return null;
        }
        return new RawMailMessage { MessageId = request.MessageId, Content = content };
    }
}

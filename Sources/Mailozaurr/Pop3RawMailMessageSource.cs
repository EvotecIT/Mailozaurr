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
        var resolved = await Pop3MailReadHandler.ResolveMessageAsync(client, identifier, cancellationToken).ConfigureAwait(false);
        if (resolved.Status == Pop3MailboxBrowser.Pop3MessageResolveStatus.NotFound) return null;
        if (resolved.Status != Pop3MailboxBrowser.Pop3MessageResolveStatus.Success || resolved.Snapshot == null) {
            throw new InvalidOperationException($"POP3 message '{request.MessageId}' could not be resolved ({resolved.Status}).");
        }

        using var stream = await client.GetStreamAsync(
            resolved.Snapshot.Index,
            headersOnly: false,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new RawMailMessage {
            MessageId = request.MessageId,
            Content = await RawMailMessageSourceUtilities.ReadBoundedAsync(stream, request.MaxBytes, cancellationToken)
                .ConfigureAwait(false)
        };
    }
}

using MimeKit;

namespace Mailozaurr.Application;

/// <summary>
/// Normalized Gmail send handler backed by Mailozaurr Gmail helpers.
/// </summary>
public sealed class GmailMailSendHandler : IMailSendHandler {
    private readonly IGmailSessionFactory _sessionFactory;
    private readonly IDraftMimeMessageFactory _draftMimeMessageFactory;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<GmailSession, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<GmailMessage>> _sendAsync;

    /// <summary>
    /// Creates a new Gmail send handler.
    /// </summary>
    public GmailMailSendHandler(
        IGmailSessionFactory sessionFactory,
        IDraftMimeMessageFactory? draftMimeMessageFactory = null,
        IPendingMessageRepository? pendingMessageRepository = null,
        Func<GmailSession, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<GmailMessage>>? sendAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _draftMimeMessageFactory = draftMimeMessageFactory ?? new DraftMimeMessageFactory();
        _pendingMessageRepository = pendingMessageRepository;
        _sendAsync = sendAsync ?? DefaultSendAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Gmail;

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (request.NotBefore.HasValue) {
            throw new NotSupportedException("Scheduled Gmail sends are not yet supported by the application send handler.");
        }

        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        var message = await _draftMimeMessageFactory.CreateAsync(profile, request.Message, cancellationToken).ConfigureAwait(false);
        var queueEnabled = _pendingMessageRepository != null && request.PreferQueue && !request.RequireImmediateSend;
        session.Client.PendingMessageRepository = queueEnabled ? _pendingMessageRepository : null;

        try {
            var providerResult = await _sendAsync(session, profile, request, message, cancellationToken).ConfigureAwait(false);
            return new SendResult {
                Succeeded = true,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                ProviderMessageId = providerResult.Id,
                QueueMessageId = message.MessageId,
                Message = "Message sent successfully."
            };
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) when (queueEnabled && !string.IsNullOrWhiteSpace(message.MessageId)) {
            var queued = await _pendingMessageRepository!.GetByMessageIdAsync(message.MessageId!, cancellationToken).ConfigureAwait(false);
            if (queued != null) {
                return new SendResult {
                    Succeeded = true,
                    ProfileId = profile.Id,
                    ProfileKind = profile.Kind,
                    Queued = true,
                    QueueMessageId = queued.MessageId,
                    Message = $"Message queued after send failure: {ex.Message}"
                };
            }

            throw;
        }
    }

    private static Task<GmailMessage> DefaultSendAsync(
        GmailSession session,
        MailProfile profile,
        SendMessageRequest request,
        MimeMessage message,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return session.Client.SendAsync(session.UserId, message, cancellationToken);
    }
}
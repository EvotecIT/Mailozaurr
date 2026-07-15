using Mailozaurr;
using MimeKit;

namespace Mailozaurr.Application;

/// <summary>
/// Normalized SMTP send handler backed by Mailozaurr SMTP helpers.
/// </summary>
public sealed class SmtpMailSendHandler : IMailSendHandler {
    private readonly ISmtpSessionFactory _sessionFactory;
    private readonly IDraftMimeMessageFactory _draftMimeMessageFactory;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<Smtp, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<SmtpResult>> _sendAsync;

    /// <summary>
    /// Creates a new SMTP send handler.
    /// </summary>
    public SmtpMailSendHandler(
        ISmtpSessionFactory sessionFactory,
        IDraftMimeMessageFactory? draftMimeMessageFactory = null,
        IPendingMessageRepository? pendingMessageRepository = null,
        Func<Smtp, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<SmtpResult>>? sendAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _draftMimeMessageFactory = draftMimeMessageFactory ?? new DraftMimeMessageFactory();
        _pendingMessageRepository = pendingMessageRepository;
        _sendAsync = sendAsync ?? DefaultSendAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Smtp;

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }
        if (request.NotBefore.HasValue) {
            throw new NotSupportedException("Scheduled SMTP sends are not yet supported by the application send handler.");
        }

        var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        try {
            var message = await _draftMimeMessageFactory.CreateAsync(profile, request.Message, cancellationToken).ConfigureAwait(false);
            var queueEnabled = _pendingMessageRepository != null && request.QueueOnFailure;
            session.PendingMessageRepository = queueEnabled ? _pendingMessageRepository : null;

            var providerResult = await _sendAsync(session, profile, request, message, cancellationToken).ConfigureAwait(false);
            if (providerResult.Status) {
                return new SendResult {
                    Succeeded = true,
                    ProfileId = profile.Id,
                    ProfileKind = profile.Kind,
                    ProviderMessageId = providerResult.MessageId ?? message.MessageId,
                    QueueMessageId = providerResult.MessageId ?? message.MessageId,
                    Message = "Message sent successfully."
                };
            }

            if (queueEnabled) {
                var queuedId = providerResult.MessageId ?? message.MessageId;
                if (!string.IsNullOrWhiteSpace(queuedId)) {
                    var queued = await _pendingMessageRepository!.GetByMessageIdAsync(queuedId!, cancellationToken).ConfigureAwait(false);
                    if (queued != null) {
                        return new SendResult {
                            Succeeded = true,
                            ProfileId = profile.Id,
                            ProfileKind = profile.Kind,
                            Queued = true,
                            QueueMessageId = queued.MessageId,
                            Message = $"Message queued after send failure: {providerResult.Error ?? providerResult.Message ?? "SMTP send failed."}"
                        };
                    }
                }
            }

            return new SendResult {
                Succeeded = false,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                ProviderMessageId = providerResult.MessageId ?? message.MessageId,
                Message = providerResult.Error ?? providerResult.Message ?? "SMTP send failed."
            };
        } finally {
            SmtpSessionService.DisposeQuietly(session);
        }
    }

    private static Task<SmtpResult> DefaultSendAsync(
        Smtp session,
        MailProfile profile,
        SendMessageRequest request,
        MimeMessage message,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        session.Message = message;
        return session.SendAsync(cancellationToken);
    }
}

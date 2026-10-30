using Mailozaurr;
using MimeKit;

namespace Mailozaurr;

/// <summary>
/// Normalized SMTP send handler backed by Mailozaurr SMTP helpers.
/// </summary>
public sealed class SmtpMailSendHandler : IMailSendHandler {
    private readonly ISmtpSessionFactory _sessionFactory;
    private readonly IDraftMimeMessageFactory _draftMimeMessageFactory;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<Smtp, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<SmtpResult>> _sendAsync;
    private readonly SmtpDeliverySession? _deliverySession;

    /// <summary>
    /// Creates an SMTP handler that reuses the caller-owned delivery session across a batch.
    /// Dispose the session after the batch to close its connections.
    /// </summary>
    public SmtpMailSendHandler(SmtpSessionFactory sessionFactory, SmtpDeliverySession deliverySession)
        : this(sessionFactory) {
        _deliverySession = deliverySession ?? throw new ArgumentNullException(nameof(deliverySession));
    }

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

        if (profile.SmtpDelivery != null) {
            return await SendWithDeliveryPolicyAsync(profile, request, cancellationToken).ConfigureAwait(false);
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

            if (providerResult.Queued) {
                var queuedId = providerResult.MessageId ?? message.MessageId;
                if (!string.IsNullOrWhiteSpace(queuedId)) {
                    return new SendResult {
                        Succeeded = true,
                        ProfileId = profile.Id,
                        ProfileKind = profile.Kind,
                        Queued = true,
                        QueueMessageId = queuedId,
                        Message = $"Message queued after send failure: {providerResult.Error ?? providerResult.Message ?? "SMTP send failed."}"
                    };
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

    private async Task<SendResult> SendWithDeliveryPolicyAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken) {
        if (request.QueueOnFailure) {
            throw new NotSupportedException("Multi-server SMTP delivery returns failed attempts directly; persist the failed request explicitly for later recovery.");
        }
        if (_sessionFactory is not SmtpSessionFactory factory) {
            throw new NotSupportedException("SMTP delivery policies require SmtpSessionFactory.");
        }
        using var message = await _draftMimeMessageFactory.CreateAsync(profile, request.Message, cancellationToken).ConfigureAwait(false);
        var transport = await factory.CreateRequestAsync(profile, cancellationToken).ConfigureAwait(false);
        var session = _deliverySession ?? new SmtpDeliverySession();
        try {
            var result = await session.SendAsync(message, transport, profile.SmtpDelivery!, cancellationToken).ConfigureAwait(false);
            return new SendResult {
                Succeeded = result.DeliveryAccepted,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                ProviderMessageId = result.MessageId ?? message.MessageId,
                Message = result.Error ?? result.Message,
                Server = result.Server,
                Port = result.Port,
                Attempts = result.Attempts,
                IsTransient = result.IsTransient,
                DeliveryUnknown = result.DeliveryUnknown
            };
        } finally {
            if (_deliverySession == null) session.Dispose();
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

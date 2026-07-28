using System.Net;

namespace Mailozaurr.Application;

/// <summary>Normalized SendGrid delivery handler backed by Mailozaurr.</summary>
public sealed class SendGridMailSendHandler : IMailSendHandler {
    private readonly IMailSecretStore _secretStore;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<SendGridClient, CancellationToken, Task<SmtpResult>> _sendAsync;

    /// <summary>Creates a SendGrid delivery handler.</summary>
    public SendGridMailSendHandler(
        IMailSecretStore secretStore,
        IPendingMessageRepository? pendingMessageRepository = null,
        Func<SendGridClient, CancellationToken, Task<SmtpResult>>? sendAsync = null) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _pendingMessageRepository = pendingMessageRepository;
        _sendAsync = sendAsync ?? DefaultSendAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.SendGrid;

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
        ProviderMailSendHandlerSupport.Validate(profile, request, Kind);
        var apiKey = await ProviderMailSendHandlerSupport.RequireSecretAsync(
            _secretStore, profile, MailSecretNames.ApiKey, cancellationToken).ConfigureAwait(false);

        using var client = new SendGridClient {
            Credentials = new NetworkCredential(string.Empty, apiKey),
            From = ProviderMailSendHandlerSupport.ToSendGridAddress(
                ProviderMailSendHandlerSupport.ResolveSender(profile, request.Message)),
            To = ProviderMailSendHandlerSupport.SendGridRecipients(request.Message.To),
            Cc = ProviderMailSendHandlerSupport.SendGridRecipients(request.Message.Cc),
            Bcc = ProviderMailSendHandlerSupport.SendGridRecipients(request.Message.Bcc),
            ReplyTo = ProviderMailSendHandlerSupport.ResolveReplyTo(request.Message) is { } replyTo
                ? ProviderMailSendHandlerSupport.ToSendGridAddress(replyTo)
                : null,
            Subject = request.Message.Subject,
            Text = request.Message.TextBody ?? string.Empty,
            Html = request.Message.HtmlBody ?? string.Empty,
            Priority = request.Message.Priority,
            Headers = request.Message.Headers,
            Attachments = ProviderMailSendHandlerSupport.Attachments(request.Message),
            PendingMessageRepository = request.QueueOnFailure ? _pendingMessageRepository : null
        };
        ProviderMailSendHandlerSupport.ApplyRetrySettings(profile, (count, delay, backoff, max, jitter, always) => {
            client.RetryCount = count;
            client.RetryDelayMilliseconds = delay;
            client.RetryDelayBackoff = backoff;
            client.MaxDelayMilliseconds = max;
            client.JitterMilliseconds = jitter;
            client.RetryAlways = always;
        });
        client.CreateMessage();

        var result = await _sendAsync(client, cancellationToken).ConfigureAwait(false);
        return ProviderMailSendHandlerSupport.ToResult(profile, result);
    }

    private static Task<SmtpResult> DefaultSendAsync(SendGridClient client, CancellationToken cancellationToken) =>
        client.SendEmailAsync(cancellationToken);
}

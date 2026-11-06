using System.Net;
using System.Net.Http;

namespace Mailozaurr;

/// <summary>Normalized SendGrid delivery handler backed by Mailozaurr.</summary>
public sealed class SendGridMailSendHandler : IMailSendHandler {
    private readonly IMailSecretStore _secretStore;
    private readonly bool _singleAttempt;

    internal SendGridMailSendHandler(IMailSecretStore secrets, bool singleAttempt) : this(secrets) => _singleAttempt = singleAttempt;
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

        using var client = CreateClient();

        client.Credentials = new NetworkCredential(string.Empty, apiKey);
        client.From = ProviderMailSendHandlerSupport.ToSendGridAddress(
                ProviderMailSendHandlerSupport.ResolveSender(profile, request.Message));
        client.To = ProviderMailSendHandlerSupport.SendGridRecipients(request.Message.To);
        client.Cc = ProviderMailSendHandlerSupport.SendGridRecipients(request.Message.Cc);
        client.Bcc = ProviderMailSendHandlerSupport.SendGridRecipients(request.Message.Bcc);
        client.ReplyTo = ProviderMailSendHandlerSupport.ResolveReplyTo(request.Message) is { } replyTo
                ? ProviderMailSendHandlerSupport.ToSendGridAddress(replyTo)
                : null;
        client.Subject = request.Message.Subject;
        client.Text = request.Message.TextBody ?? string.Empty;
        client.Html = request.Message.HtmlBody ?? string.Empty;
        client.Priority = request.Message.Priority;
        client.Headers = request.Message.Headers;
        client.Attachments = ProviderMailSendHandlerSupport.Attachments(request.Message);
        client.PendingMessageRepository = request.QueueOnFailure ? _pendingMessageRepository : null;

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

    private SendGridClient CreateClient() => _singleAttempt
        ? new SendGridClient(new HttpClientHandler { AllowAutoRedirect = false })
        : new SendGridClient();

    private static Task<SmtpResult> DefaultSendAsync(SendGridClient client, CancellationToken cancellationToken) =>
        client.SendEmailAsync(cancellationToken);
}

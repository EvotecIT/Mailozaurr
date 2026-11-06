using System.Net;
using System.Net.Http;
using MimeKit;

namespace Mailozaurr;

/// <summary>Normalized Mailgun delivery handler backed by Mailozaurr.</summary>
public sealed class MailgunMailSendHandler : IMailSendHandler {
    private readonly IMailSecretStore _secretStore;
    private readonly bool _singleAttempt;

    internal MailgunMailSendHandler(IMailSecretStore secrets, bool singleAttempt) : this(secrets) => _singleAttempt = singleAttempt;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<MailgunClient, CancellationToken, Task<SmtpResult>> _sendAsync;

    /// <summary>Creates a Mailgun delivery handler.</summary>
    public MailgunMailSendHandler(
        IMailSecretStore secretStore,
        IPendingMessageRepository? pendingMessageRepository = null,
        Func<MailgunClient, CancellationToken, Task<SmtpResult>>? sendAsync = null) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _pendingMessageRepository = pendingMessageRepository;
        _sendAsync = sendAsync ?? DefaultSendAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Mailgun;

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
        ProviderMailSendHandlerSupport.Validate(profile, request, Kind);
        var apiKey = await ProviderMailSendHandlerSupport.RequireSecretAsync(
            _secretStore, profile, MailSecretNames.ApiKey, cancellationToken).ConfigureAwait(false);
        var attachments = ProviderMailSendHandlerSupport.Attachments(request.Message);

        using var client = CreateClient();

        client.Credentials = new NetworkCredential("api", apiKey);
        client.Domain = ProviderMailSendHandlerSupport.GetSetting(profile, MailProfileSettingsKeys.Domain);
        client.From = ProviderMailSendHandlerSupport.ToMailboxAddress(
                ProviderMailSendHandlerSupport.ResolveSender(profile, request.Message));
        client.To = ProviderMailSendHandlerSupport.Recipients(request.Message.To);
        client.Cc = ProviderMailSendHandlerSupport.Recipients(request.Message.Cc);
        client.Bcc = ProviderMailSendHandlerSupport.Recipients(request.Message.Bcc);
        client.ReplyTo = ProviderMailSendHandlerSupport.ResolveReplyTo(request.Message) is { } replyTo
                ? ProviderMailSendHandlerSupport.ToMailboxAddress(replyTo)
                : null;
        client.Subject = request.Message.Subject;
        client.Text = request.Message.TextBody ?? string.Empty;
        client.Html = request.Message.HtmlBody ?? string.Empty;
        client.Priority = request.Message.Priority;
        client.Headers = request.Message.Headers;
        client.Attachments = attachments.Where(attachment =>
                !string.Equals(attachment.ContentDisposition?.Disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)).ToList();
        client.InlineAttachments = attachments.Where(attachment =>
                string.Equals(attachment.ContentDisposition?.Disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)).ToList();
        client.PendingMessageRepository = request.QueueOnFailure ? _pendingMessageRepository : null;

        ProviderMailSendHandlerSupport.ApplyRetrySettings(profile, (count, delay, backoff, max, jitter, always) => {
            client.RetryCount = count;
            client.RetryDelayMilliseconds = delay;
            client.RetryDelayBackoff = backoff;
            client.MaxDelayMilliseconds = max;
            client.JitterMilliseconds = jitter;
            client.RetryAlways = always;
        });

        var result = await _sendAsync(client, cancellationToken).ConfigureAwait(false);
        return ProviderMailSendHandlerSupport.ToResult(profile, result);
    }

    private MailgunClient CreateClient() => _singleAttempt
        ? new MailgunClient(new HttpClientHandler { AllowAutoRedirect = false })
        : new MailgunClient();

    private static Task<SmtpResult> DefaultSendAsync(MailgunClient client, CancellationToken cancellationToken) =>
        client.SendEmailAsync(cancellationToken);
}

using System.Net;
using MimeKit;

namespace Mailozaurr.Hosting;

/// <summary>Normalized Mailgun delivery handler backed by Mailozaurr.</summary>
public sealed class MailgunMailSendHandler : IMailSendHandler {
    private readonly IMailSecretStore _secretStore;
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

        using var client = new MailgunClient {
            Credentials = new NetworkCredential("api", apiKey),
            Domain = ProviderMailSendHandlerSupport.GetSetting(profile, MailProfileSettingsKeys.Domain),
            From = ProviderMailSendHandlerSupport.ToMailboxAddress(
                ProviderMailSendHandlerSupport.ResolveSender(profile, request.Message)),
            To = ProviderMailSendHandlerSupport.Recipients(request.Message.To),
            Cc = ProviderMailSendHandlerSupport.Recipients(request.Message.Cc),
            Bcc = ProviderMailSendHandlerSupport.Recipients(request.Message.Bcc),
            ReplyTo = ProviderMailSendHandlerSupport.ResolveReplyTo(request.Message) is { } replyTo
                ? ProviderMailSendHandlerSupport.ToMailboxAddress(replyTo)
                : null,
            Subject = request.Message.Subject,
            Text = request.Message.TextBody ?? string.Empty,
            Html = request.Message.HtmlBody ?? string.Empty,
            Priority = request.Message.Priority,
            Headers = request.Message.Headers,
            Attachments = attachments.Where(attachment =>
                !string.Equals(attachment.ContentDisposition?.Disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)).ToList(),
            InlineAttachments = attachments.Where(attachment =>
                string.Equals(attachment.ContentDisposition?.Disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)).ToList(),
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

        var result = await _sendAsync(client, cancellationToken).ConfigureAwait(false);
        return ProviderMailSendHandlerSupport.ToResult(profile, result);
    }

    private static Task<SmtpResult> DefaultSendAsync(MailgunClient client, CancellationToken cancellationToken) =>
        client.SendEmailAsync(cancellationToken);
}

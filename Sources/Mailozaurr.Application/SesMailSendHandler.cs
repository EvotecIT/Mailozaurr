using System.Net;
using MimeKit;

namespace Mailozaurr.Application;

/// <summary>Normalized Amazon SES delivery handler backed by Mailozaurr.</summary>
public sealed class SesMailSendHandler : IMailSendHandler {
    private readonly IMailSecretStore _secretStore;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<SesClient, CancellationToken, Task<SmtpResult>> _sendAsync;

    /// <summary>Creates an Amazon SES delivery handler.</summary>
    public SesMailSendHandler(
        IMailSecretStore secretStore,
        IPendingMessageRepository? pendingMessageRepository = null,
        Func<SesClient, CancellationToken, Task<SmtpResult>>? sendAsync = null) {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _pendingMessageRepository = pendingMessageRepository;
        _sendAsync = sendAsync ?? DefaultSendAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Ses;

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
        ProviderMailSendHandlerSupport.Validate(profile, request, Kind);
        var accessKeyId = await ProviderMailSendHandlerSupport.RequireSecretAsync(
            _secretStore, profile, MailSecretNames.AccessKeyId, cancellationToken).ConfigureAwait(false);
        var secretAccessKey = await ProviderMailSendHandlerSupport.RequireSecretAsync(
            _secretStore, profile, MailSecretNames.SecretAccessKey, cancellationToken).ConfigureAwait(false);

        using var client = new SesClient {
            Credentials = new NetworkCredential(accessKeyId, secretAccessKey),
            Region = ProviderMailSendHandlerSupport.GetSetting(profile, MailProfileSettingsKeys.Region) ?? "us-east-1",
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
            Headers = request.Message.Headers,
            Attachments = ProviderMailSendHandlerSupport.Attachments(request.Message).Where(attachment =>
                !string.Equals(attachment.ContentDisposition?.Disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase)).ToList(),
            InlineAttachments = ProviderMailSendHandlerSupport.Attachments(request.Message).Where(attachment =>
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
        return await ProviderMailSendHandlerSupport.ToResultAsync(
            profile,
            result,
            request.QueueOnFailure ? _pendingMessageRepository : null,
            cancellationToken).ConfigureAwait(false);
    }

    private static Task<SmtpResult> DefaultSendAsync(SesClient client, CancellationToken cancellationToken) =>
        client.SendEmailAsync(cancellationToken);
}

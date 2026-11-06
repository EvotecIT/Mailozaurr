using System.Net;
using System.Net.Http;
using MimeKit;

namespace Mailozaurr;

/// <summary>Normalized Amazon SES delivery handler backed by Mailozaurr.</summary>
public sealed class SesMailSendHandler : IMailSendHandler {
    private readonly IMailSecretStore _secretStore;
    private readonly bool _singleAttempt;

    internal SesMailSendHandler(IMailSecretStore secrets, bool singleAttempt) : this(secrets) => _singleAttempt = singleAttempt;
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
        var attachments = ProviderMailSendHandlerSupport.Attachments(request.Message);

        using var client = CreateClient();

        client.Credentials = new NetworkCredential(accessKeyId, secretAccessKey);
        client.Region = ProviderMailSendHandlerSupport.GetSetting(profile, MailProfileSettingsKeys.Region) ?? "us-east-1";
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

    private SesClient CreateClient() => _singleAttempt
        ? new SesClient(new HttpClientHandler { AllowAutoRedirect = false })
        : new SesClient();

    private static Task<SmtpResult> DefaultSendAsync(SesClient client, CancellationToken cancellationToken) =>
        client.SendEmailAsync(cancellationToken);
}

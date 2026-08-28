using Mailozaurr.Definitions;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// A client for sending emails using the SendGrid API.
/// </summary>
/// <remarks>
/// Only key functionality required by the module is implemented;
/// it is not intended as a full wrapper of the SendGrid SDK.
/// </remarks>
public sealed class SendGridClient : IDisposable {
    /// <summary>
    /// Gets the JSON representation of the message to be sent.
    /// </summary>
    private string MessageJson { get; set; } = string.Empty;

    /// <summary>
    /// The HttpClient used to send HTTP requests.
    /// </summary>
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private bool _disposed;
    internal TimeSpan RequestTimeout { get; set; } =
        TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stopwatch to measure the time taken to send an email.
    /// </summary>
    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// When set, sending is simulated and no SendGrid request is issued.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the sender of the email.
    /// </summary>
    public object? From { get; set; }

    /// <summary>
    /// Gets or sets the list of primary recipients of the email.
    /// </summary>
    public List<object>? To { get; set; }

    /// <summary>
    /// Gets or sets the list of carbon copy (CC) recipients of the email.
    /// </summary>
    public List<object>? Cc { get; set; }

    /// <summary>
    /// Gets or sets the list of blind carbon copy (BCC) recipients of the email.
    /// </summary>
    public List<object>? Bcc { get; set; }

    /// <summary>
    /// Gets or sets the reply-to address for the email.
    /// </summary>
    public object? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the attachments to include with the email.
    /// </summary>
    public List<AttachmentDescriptor>? Attachments { get; set; }

    /// <summary>
    /// Gets or sets inline attachments referenced from the message body.
    /// </summary>
    public List<AttachmentDescriptor>? InlineAttachments { get; set; }

    /// <summary>Custom headers to include with the message.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the plain text content of the email.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTML content of the email.
    /// </summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the priority of the email.
    /// </summary>
    public MessagePriority Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to send separate emails to each recipient.
    /// </summary>
    public bool SeparateTo { get; set; }

    /// <summary>
    /// Gets or sets the credentials used for authentication with the SendGrid API.
    /// </summary>
    public ICredentials? Credentials { get; set; }

    /// <summary>
    /// Gets or sets the action to take when an error occurs.
    /// </summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>
    /// Repository used to persist messages that require retrying.
    /// </summary>
    public IPendingMessageRepository? PendingMessageRepository { get; set; }

    /// <summary>
    /// Number of times to retry sending the message when an error occurs.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay in milliseconds between retry attempts.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Factor used to increase the delay for each subsequent retry. A value of
    /// 1 disables backoff.
    /// </summary>
    public double RetryDelayBackoff { get; set; } = 1.0;
    /// <summary>Maximum delay in milliseconds between retries. 0 disables capping.</summary>
    public int MaxDelayMilliseconds { get; set; } = 0;
    /// <summary>Jitter window in milliseconds added to retry delay. 0 disables jitter.</summary>
    public int JitterMilliseconds { get; set; } = 0;

    /// <summary>
    /// If set to <c>true</c>, retries will occur even on non-transient
    /// failures. Otherwise only transient errors trigger retries.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>Webhook invoked after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Gets a string containing the email addresses of all recipients of the email.
    /// </summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(To.Select(ConvertToEmailObject).Where(x => x != null && seen.Add(x.Email)).Select(x => x!.Email));
            }
            if (Cc != null) {
                addresses.AddRange(Cc.Select(ConvertToEmailObject).Where(x => x != null && seen.Add(x.Email)).Select(x => x!.Email));
            }
            if (Bcc != null) {
                addresses.AddRange(Bcc.Select(ConvertToEmailObject).Where(x => x != null && seen.Add(x.Email)).Select(x => x!.Email));
            }
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Gets the email address of the sender of the email.
    /// </summary>
    public string SentFrom => From != null ? Helpers.GetEmailAddress(From) : string.Empty;

    /// <summary>
    /// Gets or sets the log collector for this client.
    /// </summary>
    public LogCollector LogCollector { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the SendGridClient class.
    /// </summary>
    public SendGridClient() {
        Stopwatch = Stopwatch.StartNew();
        _client = Helpers.SharedHttpClient;
    }

    /// <summary>
    /// Initializes a client with an isolated HTTP handler.
    /// </summary>
    /// <param name="handler">HTTP handler used for provider requests.</param>
    public SendGridClient(HttpMessageHandler handler) {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler))) {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _ownsClient = true;
    }

    /// <summary>
    /// Converts the provided object to a SendGridEmailAddress object.
    /// </summary>
    /// <param name="emailAddress">The object to convert.</param>
    /// <returns>A SendGridEmailAddress object, or null if the provided object is null or an empty string.</returns>
    private SendGridEmailAddress? ConvertToEmailObject(object? emailAddress) {
        if (emailAddress == null) {
            return null;
        }

        if (emailAddress is SendGridEmailAddress sendGridEmail) {
            return sendGridEmail;
        }

        var emailAsString = Convert.ToString(emailAddress);
        if (string.IsNullOrWhiteSpace(emailAsString)) {
            return null;
        }

        if (emailAddress is string emailString) {
            return new SendGridEmailAddress { Email = emailString };
        }

        if (emailAddress is IDictionary<string, object> emailDict) {
            if (!emailDict.ContainsKey("Email")) {
                throw new ArgumentException("Dictionary is missing required key 'Email'.", nameof(emailAddress));
            }

            var emailValue = Convert.ToString(emailDict["Email"]);
            if (string.IsNullOrWhiteSpace(emailValue)) {
                return null;
            }

            var nameValue = emailDict.ContainsKey("Name") ? Convert.ToString(emailDict["Name"]) : null;
            return new SendGridEmailAddress { Email = emailValue, Name = nameValue };
        }

        throw new ArgumentException(
            $"Unsupported email address type {emailAddress.GetType().Name}. Expected string, SendGridEmailAddress, or IDictionary<string, object>.",
            nameof(emailAddress));
    }

    /// <summary>
    /// Converts a collection of attachment descriptors to <see cref="SendGridAttachment"/> objects.
    /// </summary>
    /// <param name="attachments">Attachments to convert.</param>
    /// <param name="inlineAttachments">Inline attachments to convert.</param>
    /// <param name="logger">Logger used to emit warnings.</param>
    /// <returns>List of converted attachments.</returns>
    private static List<SendGridAttachment> ConvertAttachments(
        IEnumerable<AttachmentDescriptor>? attachments,
        IEnumerable<AttachmentDescriptor>? inlineAttachments,
        LogCollector logger) {
        var result = new List<SendGridAttachment>();

        AddAttachments(attachments, inline: false, result, AttachmentPathIdentity.CreateSet());
        AddAttachments(inlineAttachments, inline: true, result, AttachmentPathIdentity.CreateSet());
        return result;
    }

    private static void AddAttachments(
        IEnumerable<AttachmentDescriptor>? attachments,
        bool inline,
        ICollection<SendGridAttachment> result,
        ISet<string> seen) {
        if (attachments == null) {
            return;
        }

        foreach (var descriptor in attachments) {
            if (descriptor == null) {
                continue;
            }

            var path = descriptor.SourcePath;
            if (!string.IsNullOrWhiteSpace(path)) {
                if (!AttachmentPathIdentity.Add(seen, path!)) {
                    continue;
                }

                if (descriptor is FileAttachmentDescriptor fileDescriptor && !File.Exists(fileDescriptor.FilePath)) {
                    throw new FileNotFoundException(
                        $"Attachment '{fileDescriptor.FilePath}' was not found.",
                        fileDescriptor.FilePath);
                }
            }

            result.Add(CreateSendGridAttachment(descriptor, inline));
        }
    }

    private static SendGridAttachment CreateSendGridAttachment(AttachmentDescriptor descriptor, bool inline) {
        if (descriptor is MimeEntityAttachmentDescriptor) {
            throw new ArgumentException("SendGrid attachments do not support MimeEntity descriptors.", nameof(descriptor));
        }

        var fileName = descriptor.FileName;
        if (string.IsNullOrWhiteSpace(fileName) && descriptor.SourcePath is string sourcePath) {
            fileName = Path.GetFileName(sourcePath);
        }

        fileName ??= "attachment";

        var contentType = descriptor.ContentType;
        if (string.IsNullOrWhiteSpace(contentType)) {
            contentType = MimeTypes.GetMimeType(fileName);
        }

        var disposition = inline
            ? ContentDisposition.Inline
            : descriptor.ContentDisposition?.Disposition ?? ContentDisposition.Attachment;
        var contentId = descriptor.ContentId;
        if (string.Equals(disposition, ContentDisposition.Inline, StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(contentId)) {
            contentId = fileName;
        }
        var bytes = descriptor.GetContentBytes();
        return new SendGridAttachment(fileName, bytes, contentType, disposition, contentId);
    }

    /// <summary>
    /// Creates a SendGridMessage object from the properties of this SendGridClient.
    /// </summary>
    public void CreateMessage() {
        ThrowIfDisposed();

        var attachments = ConvertAttachments(Attachments, InlineAttachments, LogCollector);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var personalizations = new List<SendGridPersonalization>
            {
                new SendGridPersonalization
                {
                    To = To?.Where(t => t != null)
                        .Select(ConvertToEmailObject)
                        .Where(x => x != null && seen.Add(x.Email))
                        .Select(x => x!)
                        .ToList(),
                    Cc = Cc?.Where(c => c != null)
                        .Select(ConvertToEmailObject)
                        .Where(x => x != null && seen.Add(x.Email))
                        .Select(x => x!)
                        .ToList(),
                    Bcc = Bcc?.Where(b => b != null)
                        .Select(ConvertToEmailObject)
                        .Where(x => x != null && seen.Add(x.Email))
                        .Select(x => x!)
                        .ToList()
                }
            }
            .Where(p => p.To != null || p.Cc != null || p.Bcc != null)
            .ToList();

        var content = new List<SendGridContent> {
                new SendGridContent { Type = "text/plain", Value = Text },
                new SendGridContent { Type = "text/html", Value = Html }
            }.Where(c => !string.IsNullOrEmpty(c.Value)).ToList();


        var fromAddress = ConvertToEmailObject(From) ?? throw new InvalidOperationException("From address is required.");

        var headers = Headers == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase);
        if (Priority != MessagePriority.Normal) {
            headers["X-Priority"] = Priority == MessagePriority.High ? "1" : "5";
            headers["Importance"] = Priority == MessagePriority.High ? "high" : "low";
        }

        var message = new SendGridMessage {
            Personalizations = personalizations,
            From = fromAddress,
            Subject = Subject,
            Content = content,
            ReplyTo = ConvertToEmailObject(ReplyTo),
            Attachments = attachments,
            Headers = headers.Count == 0 ? null : headers
        };

        MessageJson = JsonSerializer.Serialize(message, MailozaurrJsonContext.Default.SendGridMessage);
        //Console.WriteLine(MessageJson);
    }

    /// <summary>
    /// Sends an email asynchronously using the SendGrid API.
    /// </summary>
    /// <returns>A Task that represents the asynchronous operation. The task result contains the result of the email sending operation.</returns>
    public Task<SmtpResult> SendEmailAsync() => SendEmailAsync(CancellationToken.None);

    /// <summary>
    /// Sends an email asynchronously using the SendGrid API.
    /// </summary>
    /// <returns>A Task that represents the asynchronous operation. The task result contains the result of the email sending operation.</returns>
    public async Task<SmtpResult> SendEmailAsync(CancellationToken cancellationToken) {
        try {
            return await SendEmailCoreAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            AttachmentDescriptorLifetime.ReleaseStaging(Attachments, InlineAttachments);
        }
    }

    private async Task<SmtpResult> SendEmailCoreAsync(CancellationToken cancellationToken) {
        ThrowIfDisposed();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping SendGrid send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        string apiKey;
        if (Credentials is NetworkCredential networkCredential) {
            apiKey = networkCredential.Password;
        } else {
            const string message = "Credentials must be NetworkCredential";
            LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SendGrid: {message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidCastException(message);
            }
            var credFail = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, string.Empty, message);
            await Helpers.PostWebhookAsync(WebhookUrl, credFail, cancellationToken).ConfigureAwait(false);
            return credFail;
        }

        int attempts = 0;
        Exception? lastException = null;
        string? lastContent = null;
        while (true) {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send") {
                    Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                using var requestTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                requestTimeout.CancelAfter(RequestTimeout);
                using var response = await _client.SendAsync(
                    request,
                    requestTimeout.Token).ConfigureAwait(false);
                lastContent = await ProviderResponseParser
                    .ReadContentAsync(response, cancellationToken)
                    .ConfigureAwait(false);
                LogCollector.LogVerbose($"Send-EmailMessage - Sent email to {SentTo} using SendGrid");

                if (response.IsSuccessStatusCode) {
                    var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, response.StatusCode.ToString()) {
                        MessageId = ProviderResponseParser
                            .GetSendGridMessageId(response)
                    };
                    await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken).ConfigureAwait(false);
                    return okResult;
                }

                var message = $"Status code {response.StatusCode}: {lastContent}";
                throw HttpRetryPolicy.CreateFailure(response.StatusCode, message);
            } catch (HttpRequestException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - HTTP error during sending using SendGrid: {ex.Message}");
                if (!HttpRetryPolicy.ShouldRetry(ex, attempts, RetryCount, RetryAlways)) {
                    var queuedMessageId =
                        await QueuePendingMessageAsync(
                            apiKey,
                            cancellationToken).ConfigureAwait(false);
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, ex.Message) {
                        MessageId = queuedMessageId,
                        Queued = queuedMessageId != null
                    };
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken).ConfigureAwait(false);
                    return failResult;
                }

                await HttpRetryPolicy.DelayAsync(
                    RetryDelayMilliseconds,
                    RetryDelayBackoff,
                    attempts,
                    MaxDelayMilliseconds,
                    JitterMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Request canceled: {ex.Message}");
                if (!HttpRetryPolicy.ShouldRetry(
                        ex,
                        attempts,
                        RetryCount,
                        RetryAlways,
                        isKnownTransient: true)) {
                    var queuedMessageId =
                        await QueuePendingMessageAsync(
                            apiKey,
                            cancellationToken).ConfigureAwait(false);
                    if (ErrorAction == ActionPreference.Stop && lastException != null) {
                        ExceptionDispatchInfo.Capture(lastException).Throw();
                        throw new InvalidOperationException("The SendGrid failure could not be rethrown.");
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message) {
                        MessageId = queuedMessageId,
                        Queued = queuedMessageId != null
                    };
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken).ConfigureAwait(false);
                    return failResult;
                }
                await HttpRetryPolicy.DelayAsync(
                    RetryDelayMilliseconds,
                    RetryDelayBackoff,
                    attempts,
                    MaxDelayMilliseconds,
                    JitterMilliseconds,
                    cancellationToken).ConfigureAwait(false);
            }
            attempts++;
        }
    }

    private async Task<string?> QueuePendingMessageAsync(string apiKey, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return null;
        }

        if (string.IsNullOrWhiteSpace(MessageJson)) {
            return null;
        }

        if (string.IsNullOrEmpty(apiKey)) {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var record = new PendingMessageRecord {
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = now,
            NextAttemptAt = now,
            Provider = EmailProvider.SendGrid
        };
        record.ProviderData[SendGridPendingMessageSender.MessageJsonKey] = MessageJson;
        var protector = CredentialProtection.Default;
        record.ProviderData[SendGridPendingMessageSender.ApiKeyProtectedKey] = protector.Protect(apiKey);
        record.ProviderData.Remove(SendGridPendingMessageSender.ApiKeyKey);
        record.ProviderData.Remove(SendGridPendingMessageSender.ApiKeyBase64Key);

        try {
            await PendingMessageRepository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
            return record.MessageId;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Failed to persist SendGrid pending message: {ex.Message}");
            return null;
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(SendGridClient));
        }
    }

    /// <summary>
    /// Releases resources used by the <see cref="SendGridClient"/>.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing) {
        if (_disposed) {
            return;
        }

        if (disposing) AttachmentDescriptorLifetime.ReleaseStaging(Attachments, InlineAttachments);
        if (disposing && _ownsClient) {
            _client.Dispose();
        }

        _disposed = true;
    }
}

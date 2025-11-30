using System.Threading;
using Mailozaurr.Definitions;

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
    private bool _disposed;

    /// <summary>
    /// Stopwatch to measure the time taken to send an email.
    /// </summary>
    public readonly Stopwatch Stopwatch;

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
        _client = new HttpClient {
            Timeout = TimeSpan.FromSeconds(30)
        };
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
    /// <param name="logger">Logger used to emit warnings.</param>
    /// <returns>List of converted attachments.</returns>
    private static List<SendGridAttachment> ConvertAttachments(IEnumerable<AttachmentDescriptor>? attachments, LogCollector logger) {
        var result = new List<SendGridAttachment>();
        if (attachments == null) {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in attachments) {
            if (descriptor == null) {
                continue;
            }

            var path = descriptor.SourcePath;
            if (!string.IsNullOrWhiteSpace(path)) {
                if (!seen.Add(path!)) {
                    continue;
                }

                if (descriptor is FileAttachmentDescriptor fileDescriptor && !File.Exists(fileDescriptor.FilePath)) {
                    logger.LogWarning($"Send-EmailMessage - File not found: {fileDescriptor.FilePath}. Skipping attachment.");
                    continue;
                }
            }

            result.Add(CreateSendGridAttachment(descriptor));
        }

        return result;
    }

    private static SendGridAttachment CreateSendGridAttachment(AttachmentDescriptor descriptor) {
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

        var disposition = descriptor.ContentDisposition?.Disposition ?? "attachment";
        var bytes = descriptor.GetContentBytes();
        return new SendGridAttachment(fileName, bytes, contentType, disposition, descriptor.ContentId);
    }

    /// <summary>
    /// Creates a SendGridMessage object from the properties of this SendGridClient.
    /// </summary>
    public void CreateMessage() {
        ThrowIfDisposed();

        var attachments = ConvertAttachments(Attachments, LogCollector);

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

        var message = new SendGridMessage {
            Personalizations = personalizations,
            From = fromAddress,
            Subject = Subject,
            Content = content,
            ReplyTo = ConvertToEmailObject(ReplyTo),
            Attachments = attachments,
            Headers = Headers
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
        ThrowIfDisposed();
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
        do {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send") {
                    Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
#if NET5_0_OR_GREATER
                lastContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                lastContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                LogCollector.LogVerbose($"Send-EmailMessage - Sent email to {SentTo} using SendGrid");

                if (response.IsSuccessStatusCode) {
                    var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, response.StatusCode.ToString());
                    await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken).ConfigureAwait(false);
                    return okResult;
                }

                var message = $"Status code {response.StatusCode}: {lastContent}";
                lastException = new HttpRequestException(message);
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using SendGrid: {message}");
            } catch (HttpRequestException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - HTTP error during sending using SendGrid: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    await QueuePendingMessageAsync(apiKey, cancellationToken).ConfigureAwait(false);
                    if (ErrorAction == ActionPreference.Stop && lastException != null) {
                        throw lastException;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken).ConfigureAwait(false);
                    return failResult;
                }

                var delayMilliseconds = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (MaxDelayMilliseconds > 0 && delayMilliseconds > MaxDelayMilliseconds) {
                    delayMilliseconds = MaxDelayMilliseconds;
                }
                if (JitterMilliseconds > 0 && delayMilliseconds > 0) {
                    delayMilliseconds += GraphRetryHelperRandom.NextInt(JitterMilliseconds + 1);
                }
                if (delayMilliseconds > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken).ConfigureAwait(false);
                }
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Request canceled: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    await QueuePendingMessageAsync(apiKey, cancellationToken).ConfigureAwait(false);
                    if (ErrorAction == ActionPreference.Stop && lastException != null) {
                        throw lastException;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken).ConfigureAwait(false);
                    return failResult;
                }
                var delayMs = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (MaxDelayMilliseconds > 0 && delayMs > MaxDelayMilliseconds) {
                    delayMs = MaxDelayMilliseconds;
                }
                if (JitterMilliseconds > 0 && delayMs > 0) {
                    delayMs += GraphRetryHelperRandom.NextInt(JitterMilliseconds + 1);
                }
                if (delayMs > 0) {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken).ConfigureAwait(false);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        await QueuePendingMessageAsync(apiKey, cancellationToken).ConfigureAwait(false);
        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "SendGridApi", 0, Stopwatch.Elapsed, lastContent, lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken).ConfigureAwait(false);
        return finalResult;
    }

    private async Task QueuePendingMessageAsync(string apiKey, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return;
        }

        if (string.IsNullOrWhiteSpace(MessageJson)) {
            return;
        }

        if (string.IsNullOrEmpty(apiKey)) {
            return;
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
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Failed to persist SendGrid pending message: {ex.Message}");
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

        if (disposing) {
            _client.Dispose();
        }

        _disposed = true;
    }
}

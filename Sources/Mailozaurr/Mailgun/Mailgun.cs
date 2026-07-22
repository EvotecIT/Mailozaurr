using Mailozaurr.Definitions;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Simple client for sending emails using the Mailgun API.
/// </summary>
public class MailgunClient : IDisposable {
    private readonly HttpClient _client;
    private int _disposed;
    /// <summary>Measures total time spent sending.</summary>
    public readonly Stopwatch Stopwatch;

    private string ApiKey {
        get {
            try {
                return Helpers.CredentialToApiKey(Credentials);
            } catch (ArgumentException ex) {
                throw new InvalidOperationException("Credentials must be a NetworkCredential", ex);
            }
        }
    }
    private string EmailDomain {
        get {
            if (!string.IsNullOrWhiteSpace(Domain)) {
                return Domain!.Trim();
            }
            var address = Helpers.GetEmailAddress(From);
            if (!address.Contains('@')) {
                throw new ArgumentException($"Invalid email address: {address}", nameof(From));
            }
            return address.Split('@')[1];
        }
    }

    /// <summary>Credentials used to authenticate to the API.</summary>
    /// <remarks>Must be <see cref="NetworkCredential"/>.</remarks>
    public ICredentials Credentials { get; set; } = null!;
    /// <summary>Optional explicit Mailgun sending domain. Defaults to the sender address domain.</summary>
    public string? Domain { get; set; }
    /// <summary>Determines how errors are handled.</summary>
    public ActionPreference? ErrorAction { get; set; }

    /// <summary>Primary recipients.</summary>
    public List<object> To { get; set; } = new();
    /// <summary>Carbon copy recipients.</summary>
    public List<object> Cc { get; set; } = new();
    /// <summary>Blind carbon copy recipients.</summary>
    public List<object> Bcc { get; set; } = new();
    /// <summary>The sender address.</summary>
    public object From { get; set; } = null!;
    /// <summary>Reply-to address.</summary>
    public object? ReplyTo { get; set; }
    /// <summary>Message subject.</summary>
    public string? Subject { get; set; }
    /// <summary>Plain text body.</summary>
    public string Text { get; set; } = string.Empty;
    /// <summary>HTML body.</summary>
    public string Html { get; set; } = string.Empty;
    /// <summary>File paths to include as attachments.</summary>
    public string[]? Attachment { get; set; }
    /// <summary>File paths to include as inline attachments.</summary>
    public string[]? InlineAttachment { get; set; }
    /// <summary>Structured attachments to include.</summary>
    public List<AttachmentDescriptor>? Attachments { get; set; }
    /// <summary>Structured inline attachments to include.</summary>
    public List<AttachmentDescriptor>? InlineAttachments { get; set; }

    /// <summary>Custom headers to include with the message.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>Collector used to store log entries.</summary>
    public LogCollector LogCollector { get; set; } = new();

    /// <summary>
    /// When set, sending is simulated and no Mailgun request is issued.
    /// </summary>
    public bool DryRun { get; set; }
    /// <summary>Number of retry attempts on failure.</summary>
    public int RetryCount { get; set; } = 0;
    /// <summary>Base delay in milliseconds between retries.</summary>
    public int RetryDelayMilliseconds { get; set; } = 0;
    /// <summary>Exponential backoff multiplier for retries.</summary>
    public double RetryDelayBackoff { get; set; } = 1.0;
    /// <summary>Maximum delay in milliseconds between retries. 0 disables capping.</summary>
    public int MaxDelayMilliseconds { get; set; } = 0;
    /// <summary>Jitter window in milliseconds added to retry delay. 0 disables jitter.</summary>
    public int JitterMilliseconds { get; set; } = 0;

    /// <summary>
    /// When set to <c>true</c> the client retries sending even if the
    /// encountered error is not transient.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>URL of the webhook called after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Repository used to persist messages that need retrying.</summary>
    public IPendingMessageRepository? PendingMessageRepository { get; set; }

    /// <summary>The normalized sender email address.</summary>
    public string SentFrom => Helpers.GetEmailAddress(From);
    /// <summary>Comma separated list of recipients.</summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) addresses.AddRange(Helpers.UniqueAddresses(To, seen).Select(Helpers.GetEmailAddress));
            if (Cc != null) addresses.AddRange(Helpers.UniqueAddresses(Cc, seen).Select(Helpers.GetEmailAddress));
            if (Bcc != null) addresses.AddRange(Helpers.UniqueAddresses(Bcc, seen).Select(Helpers.GetEmailAddress));
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MailgunClient"/> class.
    /// </summary>
    public MailgunClient() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
    }

    /// <summary>
    /// Converts an address object into the format required by the Mailgun API.
    /// </summary>
    /// <param name="address">The address object to convert.</param>
    /// <returns>The formatted address string.</returns>
    private static string ConvertAddress(object address) {
        var (email, name) = Helpers.GetEmailAndName(address);
        var emailSafe = email ?? string.Empty;
        return string.IsNullOrWhiteSpace(name) ? emailSafe : $"{name} <{emailSafe}>";
    }

    private static StreamContent CreateStreamContent(string path) {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return streamContent;
    }

    private static StreamContent CreateStreamContent(AttachmentDescriptor descriptor) {
        Stream stream = descriptor.SourcePath is { Length: > 0 } sourcePath
            ? new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8192,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan)
            : new MemoryStream(descriptor.GetContentBytes(), writable: false);
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(descriptor.ContentType)
                ? "application/octet-stream"
                : descriptor.ContentType);
        if (!string.IsNullOrWhiteSpace(descriptor.ContentId)) {
            streamContent.Headers.TryAddWithoutValidation("Content-ID", descriptor.ContentId);
        }
        return streamContent;
    }

    /// <summary>
    /// Builds the multipart HTTP content used for the Mailgun API request.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel asynchronous operations.</param>
    /// <returns>The constructed multipart content.</returns>
    private Task<MultipartFormDataContent> CreateContentAsync(CancellationToken cancellationToken) {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(ConvertAddress(From)), "from");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in Helpers.UniqueAddresses(To, seen)) content.Add(new StringContent(ConvertAddress(t)), "to");
        foreach (var c in Helpers.UniqueAddresses(Cc, seen)) content.Add(new StringContent(ConvertAddress(c)), "cc");
        foreach (var b in Helpers.UniqueAddresses(Bcc, seen)) content.Add(new StringContent(ConvertAddress(b)), "bcc");
        if (ReplyTo != null) content.Add(new StringContent(ConvertAddress(ReplyTo)), "h:Reply-To");
        if (!string.IsNullOrWhiteSpace(Subject)) content.Add(new StringContent(Subject), "subject");
        if (!string.IsNullOrWhiteSpace(Text)) content.Add(new StringContent(Text), "text");
        if (!string.IsNullOrWhiteSpace(Html)) content.Add(new StringContent(Html), "html");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Attachment != null) {
            foreach (var path in Attachment) {
                if (!files.Add(path)) continue;
                if (!File.Exists(path)) {
                    LogCollector.LogWarning($"Send-EmailMessage - Attachment file not found: {path}");
                    LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{path}' is invalid. Verify the file exists and the path is correct.");
                    continue;
                }

                var fileContent = CreateStreamContent(path);
                content.Add(fileContent, "attachment", Path.GetFileName(path));
            }
        }
        if (InlineAttachment != null) {
            foreach (var path in InlineAttachment) {
                if (!files.Add(path)) continue;
                if (!File.Exists(path)) {
                    LogCollector.LogWarning($"Send-EmailMessage - Inline attachment file not found: {path}");
                    LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{path}' is invalid. Verify the file exists and the path is correct.");
                    continue;
                }

                var fileContent = CreateStreamContent(path);
                content.Add(fileContent, "inline", Path.GetFileName(path));
            }
        }
        AddStructuredAttachments(content, Attachments, "attachment");
        AddStructuredAttachments(content, InlineAttachments, "inline");
        if (Headers != null) {
            foreach (var kvp in Headers) {
                content.Add(new StringContent(kvp.Value), $"h:{kvp.Key}");
            }
        }
        return Task.FromResult(content);
    }

    private void AddStructuredAttachments(
        MultipartFormDataContent content,
        IEnumerable<AttachmentDescriptor>? attachments,
        string fieldName) {
        if (attachments == null) return;
        foreach (var descriptor in attachments) {
            if (descriptor is FileAttachmentDescriptor fileDescriptor && !File.Exists(fileDescriptor.FilePath)) {
                LogCollector.LogWarning($"Send-EmailMessage - Attachment file not found: {fileDescriptor.FilePath}");
                LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{fileDescriptor.FilePath}' is invalid. Verify the file exists and the path is correct.");
                continue;
            }

            var fileName = string.IsNullOrWhiteSpace(descriptor.FileName)
                ? Path.GetFileName(descriptor.SourcePath) ?? "attachment"
                : descriptor.FileName!;
            content.Add(CreateStreamContent(descriptor), fieldName, fileName);
        }
    }

    private MimeMessage BuildMimeMessage() {
        var smtp = new Smtp {
            From = From,
            To = To,
            Cc = Cc,
            Bcc = Bcc,
            ReplyTo = ReplyTo,
            Subject = Subject ?? string.Empty,
            TextBody = Text,
            HtmlBody = Html,
            Headers = Headers
        };
        if (Attachment != null) {
            smtp.Attachments = Attachment.Select(path => new FileAttachmentDescriptor(path)).Cast<AttachmentDescriptor>().ToList();
        }
        if (InlineAttachment != null) {
            smtp.InlineAttachments = InlineAttachment.Select(path => new FileAttachmentDescriptor(path)).Cast<AttachmentDescriptor>().ToList();
        }
        if (Attachments != null) {
            smtp.Attachments ??= new List<AttachmentDescriptor>();
            smtp.Attachments.AddRange(Attachments);
        }
        if (InlineAttachments != null) {
            smtp.InlineAttachments ??= new List<AttachmentDescriptor>();
            smtp.InlineAttachments.AddRange(InlineAttachments);
        }
        smtp.CreateMessage();
        return smtp.Message;
    }

    private async Task<string?> QueuePendingMessageAsync(CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return null;
        }

        string apiKey;
        string domain;
        try {
            apiKey = ApiKey;
            domain = EmailDomain;
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Failed to capture Mailgun credentials for retry: {ex.Message}");
            return null;
        }

        if (string.IsNullOrEmpty(apiKey)) {
            return null;
        }

        MimeMessage message;
        try {
            message = BuildMimeMessage();
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Failed to serialize Mailgun message for retry: {ex.Message}");
            return null;
        }

        var messageId = string.IsNullOrEmpty(message.MessageId)
            ? MimeKit.Utils.MimeUtils.GenerateMessageId(domain)
            : message.MessageId!;

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var record = new PendingMessageRecord {
            MessageId = messageId,
            MimeMessage = Convert.ToBase64String(stream.ToArray()),
            Timestamp = now,
            NextAttemptAt = now,
            Provider = EmailProvider.Mailgun
        };
        record.ProviderData[MailgunPendingMessageSender.DomainKey] = domain;
        var protector = CredentialProtection.Default;
        record.ProviderData[MailgunPendingMessageSender.ApiKeyProtectedKey] = protector.Protect(apiKey);
        record.ProviderData.Remove(MailgunPendingMessageSender.ApiKeyKey);
        record.ProviderData.Remove(MailgunPendingMessageSender.ApiKeyBase64Key);

        try {
            await PendingMessageRepository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
            return record.MessageId;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Failed to persist Mailgun pending message: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sends the email using the Mailgun REST API.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public Task<SmtpResult> SendEmailAsync() {
        ThrowIfDisposed();
        return SendEmailAsync(CancellationToken.None);
    }

    /// <summary>
    /// Sends the email using the Mailgun REST API.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendEmailAsync(CancellationToken cancellationToken) {
        ThrowIfDisposed();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Mailgun send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        var url = $"https://api.mailgun.net/v3/{EmailDomain}/messages";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{ApiKey}"));

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                using var content = await CreateContentAsync(cancellationToken).ConfigureAwait(false);
                using var request = new HttpRequestMessage(HttpMethod.Post, url) {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) {
                    var statusCode = response.StatusCode.ToString();
                    var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, statusCode, "");
                    await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken).ConfigureAwait(false);
                    return okResult;
                }
#if NET5_0_OR_GREATER
                var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                throw new HttpRequestException(error);
            } catch (Exception ex) when (
                ex is HttpRequestException ||
                ex is TaskCanceledException && !cancellationToken.IsCancellationRequested) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Mailgun: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    var queuedMessageId = await QueuePendingMessageAsync(cancellationToken).ConfigureAwait(false);
                    if (ErrorAction == ActionPreference.Stop) throw;
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, "", ex.Message) {
                        MessageId = queuedMessageId
                    };
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken).ConfigureAwait(false);
                    return failResult;
                }
                var delay = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
                if (MaxDelayMilliseconds > 0 && delay > MaxDelayMilliseconds) {
                    delay = MaxDelayMilliseconds;
                }
                if (JitterMilliseconds > 0 && delay > 0) {
                    delay += GraphRetryHelperRandom.NextInt(JitterMilliseconds + 1);
                }
                if (delay > 0) await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken).ConfigureAwait(false);
            }
            attempts++;
        } while (attempts <= RetryCount);
        var finalQueuedMessageId = await QueuePendingMessageAsync(cancellationToken).ConfigureAwait(false);
        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "MailgunApi", 0, Stopwatch.Elapsed, "", lastException?.Message) {
            MessageId = finalQueuedMessageId
        };
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken).ConfigureAwait(false);
        return finalResult;
    }

    /// <summary>
    /// Releases resources used by the client.
    /// </summary>
    private void ThrowIfDisposed() {
        if (Volatile.Read(ref _disposed) != 0) {
            throw new ObjectDisposedException(nameof(MailgunClient));
        }
    }

    /// <summary>Releases resources used by the client.</summary>
    /// <param name="disposing">When true, disposes managed resources as well.</param>
    protected virtual void Dispose(bool disposing) {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (disposing) {
            _client.Dispose();
        }
    }

    /// <summary>Finalizer that ensures unmanaged resources are released.</summary>
    ~MailgunClient() => Dispose(false);

    /// <summary>Disposes the client and suppresses finalization.</summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

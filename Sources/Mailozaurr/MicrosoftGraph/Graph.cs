using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

/// <summary>
/// Helper class for sending messages via Microsoft Graph API.
/// </summary>
/// <remarks>
/// Provides convenience methods for constructing requests and
/// uploading attachments without having to manually craft HTTP calls.
/// </remarks>
public partial class Graph : IDisposable {
    private readonly HttpClient _client;
    /// <summary>
    /// Maximum size of an attachment chunk when uploading large files (4 MiB).
    /// </summary>
    public const int MaxChunkSize = 4 * 1024 * 1024;
    private const long GraphPayloadLimitBytes = 4_000_000;
    private int _chunkSize = MaxChunkSize;
    /// <summary>
    /// Serialized JSON representation of the current Graph message.
    /// </summary>
    public string MessageJson = string.Empty;

    /// <summary>
    /// Container object used when building a Graph message.
    /// </summary>
    public GraphMessageContainer MessageContainer = new();

    /// <summary>Measures elapsed time spent during send operations.</summary>
    public readonly Stopwatch Stopwatch;

    /// <summary>
    /// Value indicating whether the total size of the attachments is larger than the Graph payload limit.
    /// </summary>
    public bool IsLargerAttachment { get; set; }

    /// <summary>
    /// Total size of all attachments in bytes (including file paths and in-memory attachments).
    /// </summary>
    public long TotalAttachmentSizeBytes { get; private set; }

    private long _inlineAttachmentSizeBytes;
    private int _fileAttachmentCount;

    /// <summary>
    /// List of GraphAttachment objects created from the file paths in the Attachments property.
    /// </summary>
    public List<GraphAttachment> ConvertedAttachments { get; set; } = new List<GraphAttachment>();

    /// <summary>
    /// Collection of attachment placeholders used for large file uploads.
    /// </summary>
    public List<GraphAttachmentPlaceHolder> AttachmentsPlaceHolders { get; set; } = new List<GraphAttachmentPlaceHolder>();

    /// <summary>
    /// Collection of attachments which can be file paths or GraphAttachment objects.
    /// </summary>
    public object[]? Attachments { get; set; }

    /// <summary>
    /// Gets or sets the sender. Can be a string (email) or a dictionary with Name and Email.
    ///
    /// Note: The display name ("Name") for the sender is controlled by Office 365 and may not reflect the value you provide here.
    /// Office 365 will use the mailbox's configured display name for the sender, regardless of what is set in the payload.
    /// The email address must be used for API calls and authentication.
    /// </summary>
    public object? From { get; set; }

    /// <summary>
    /// Gets or sets the email address to reply to.
    /// </summary>
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the email addresses of the recipients.
    /// </summary>
    public object[]? To { get; set; }

    /// <summary>
    /// Gets or sets the email addresses of the CC recipients.
    /// </summary>
    public object[]? Cc { get; set; }

    /// <summary>
    /// Gets or sets the email addresses of the BCC recipients.
    /// </summary>
    public object[]? Bcc { get; set; }

    /// <summary>
    /// Gets or sets the subject of the email.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// HTML content of the email.
    /// </summary>
    public string HTML { get; set; } = string.Empty;

    /// <summary>
    /// Priority of the message (mapped to Graph importance).
    /// </summary>
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;

    private string _contentType = "HTML";

    /// <summary>
    /// Content type of the email.
    /// </summary>
    public string ContentType {
        get => _contentType;
        set {
            if (value is null) {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.Equals(value, "HTML", StringComparison.OrdinalIgnoreCase)) {
                _contentType = "HTML";
                return;
            }

            if (string.Equals(value, "Text", StringComparison.OrdinalIgnoreCase)) {
                _contentType = "Text";
                return;
            }

            throw new ArgumentException("ContentType must be either \"Text\" or \"HTML\".", nameof(value));
        }
    }

    /// <summary>
    /// Value indicating whether the message should not be saved to the Sent Items folder.
    /// </summary>
    public bool DoNotSaveToSentItems { get; set; }

    /// <summary>
    /// Access token for the Graph API.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Application ID for the Graph API.
    /// </summary>
    private string ApplicationID { get; set; } = string.Empty;

    /// <summary>
    /// Application key for the Graph API.
    /// </summary>
    private string ApplicationKey { get; set; } = string.Empty;

    /// <summary>
    /// Tenant domain for the Graph API.
    /// </summary>
    private string TenantDomain { get; set; } = string.Empty;

    /// <summary>
    /// Action to take when an error occurs based on the ErrorAction preference.
    /// </summary>
    public ActionPreference? ErrorAction { get; set; }

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

    /// <summary>
    /// Timeout for HTTP operations in seconds.
    /// </summary>
    public int TimeoutSeconds {
        get => (int)_client.Timeout.TotalSeconds;
        set => _client.Timeout = TimeSpan.FromSeconds(value);
    }

    /// <summary>
    /// When enabled, scans the HTML body for local image references and embeds
    /// them as inline attachments.
    /// </summary>
    public bool AutoEmbedImages { get; set; } = false;

    /// <summary>
    /// Forces retries even when the encountered error is not classified as
    /// transient.
    /// </summary>
    public bool RetryAlways { get; set; } = false;

    /// <summary>
    /// Optional policy controlling throttling/backoff and fallback behavior for Graph sends.
    /// </summary>
    public GraphSendPolicy? SendPolicy { get; private set; }

    private Func<Smtp>? _smtpFallbackFactory;

    /// <summary>Webhook invoked after sending.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Custom headers to include with the message.</summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Size in bytes of the chunks used when uploading attachments. Defaults to
    /// <see cref="MaxChunkSize"/> and cannot exceed this value.
    /// </summary>
    public int ChunkSize {
        get => _chunkSize;
        set => _chunkSize = value > MaxChunkSize ? MaxChunkSize : value;
    }

    /// <summary>
    /// The type of token that was issued.
    /// </summary>
    public string TokenType { get; set; } = string.Empty;

    /// <summary>
    /// The email address that the message was sent from.
    /// </summary>
    public string SentFrom => From == null ? string.Empty : Helpers.GetEmailAddress(From);

    /// <summary>
    /// A comma-separated list of email addresses that the message was sent to.
    /// </summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(Helpers.UniqueAddresses(To, seen).Select(obj => Helpers.GetEmailAddress(obj)));
            }
            if (Cc != null) {
                addresses.AddRange(Helpers.UniqueAddresses(Cc, seen).Select(obj => Helpers.GetEmailAddress(obj)));
            }
            if (Bcc != null) {
                addresses.AddRange(Helpers.UniqueAddresses(Bcc, seen).Select(obj => Helpers.GetEmailAddress(obj)));
            }
            return string.Join(",", addresses);
        }
    }

    /// <summary>
    /// Request a read receipt for the message.
    /// </summary>
    public bool RequestReadReceipt { get; set; }

    /// <summary>
    /// Request a delivery receipt for the message.
    /// </summary>
    public bool RequestDeliveryReceipt { get; set; }

    /// <summary>Collector used to store log entries.</summary>
    public LogCollector LogCollector { get; set; } = new();

    /// <summary>
    /// When set, sending is simulated and no Graph requests are issued.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Initializes a new instance of the Graph class.
    /// </summary>
    public Graph() {
        Stopwatch = Stopwatch.StartNew();
        _client = new HttpClient();
        _client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        if (LogCollector == null) LogCollector = new();
    }

    /// <summary>
    /// Apply a send policy to this instance. Also updates the global Graph concurrency limit.
    /// </summary>
    /// <remarks>
    /// The concurrency limit is process-wide and affects all Graph operations within the AppDomain.
    /// The update is performed using a thread-safe semaphore swap, but callers should be aware of
    /// the global nature of this setting when running multiple independent pipelines in parallel.
    /// </remarks>
    public Graph WithSendPolicy(GraphSendPolicy policy) {
        SendPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (policy.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policy.MaxConcurrency;
        }
        return this;
    }

    /// <summary>
    /// Provide an SMTP factory used for fallback when the active policy enables SMTP fallback.
    /// </summary>
    public Graph WithSmtpFallback(Func<Smtp> factory) {
        _smtpFallbackFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    private void LogMissingAttachmentWarning(string attachmentPath) {
        var pathForMessage = string.IsNullOrWhiteSpace(attachmentPath)
            ? "(empty path)"
            : attachmentPath;
        LogCollector.LogWarning($"Send-EmailMessage - Attachment file not found: {pathForMessage}");
        LogCollector.LogWarning($"Send-EmailMessage - Possible issue: Path '{pathForMessage}' is invalid. Verify the file exists and the path is correct.");
    }

    private Stopwatch StartOperationTimer() {
        Stopwatch.Reset();
        Stopwatch.Start();
        return System.Diagnostics.Stopwatch.StartNew();
    }

    private async Task WaitForConcurrencyAsync(Stopwatch operationStopwatch, CancellationToken cancellationToken) {
        operationStopwatch.Stop();
        Stopwatch.Stop();
        try {
            await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        } finally {
            Stopwatch.Start();
            operationStopwatch.Start();
        }
    }






    private async Task<SmtpResult> TrySmtpFallbackAsync(GraphSendPolicy? policy, SmtpResult current, Exception? lastException, CancellationToken cancellationToken) {
        if (policy == null || !policy.EnableSmtpFallback) {
            return current;
        }

        var smtp = _smtpFallbackFactory?.Invoke() ?? MailozaurrOptions.SmtpFallbackFactory?.Invoke(this);
        if (smtp == null) {
            LogCollector.LogVerbose("Send-EmailMessage - SMTP fallback requested but no SMTP factory configured.");
            return current;
        }

        try {
            smtp.From = this.From;
            smtp.To = this.To;
            smtp.Cc = this.Cc;
            smtp.Bcc = this.Bcc;
            smtp.ReplyTo = string.IsNullOrWhiteSpace(this.ReplyTo) ? null : this.ReplyTo;
            smtp.Subject = this.Subject;
            ApplyBodyToSmtpFallback(smtp);
            smtp.Headers = this.Headers;
            smtp.WebhookUrl = this.WebhookUrl;
            smtp.Priority = this.Priority;

            if (this.ConvertedAttachments != null && this.ConvertedAttachments.Count > 0) {
                var attachments = new List<Definitions.AttachmentDescriptor>();
                var inline = new List<Definitions.AttachmentDescriptor>();
                foreach (var a in this.ConvertedAttachments) {
                    if (string.IsNullOrWhiteSpace(a.ContentBytes)) continue;
                    try {
                        var bytes = Convert.FromBase64String(a.ContentBytes);
                        var d = new Definitions.ByteArrayAttachmentDescriptor(bytes, string.IsNullOrWhiteSpace(a.Name) ? DefaultAttachmentName : a.Name);
                        if (!string.IsNullOrWhiteSpace(a.ContentId)) d.ContentId = a.ContentId;
                        if (a.IsInline) inline.Add(d); else attachments.Add(d);
                    } catch (FormatException fex) {
                        LogCollector.LogWarning($"Send-EmailMessage - SMTP fallback skipped invalid base64 attachment '{(a?.Name ?? "(unnamed)")}' : {fex.Message}");
                    }
                }
                if (attachments.Count > 0) smtp.Attachments = attachments;
                if (inline.Count > 0) smtp.InlineAttachments = inline;
            }
            if (IsLargerAttachment) {
                smtp.Attachments ??= new List<Definitions.AttachmentDescriptor>();
                foreach (var source in EnumerateFileAttachmentSources()) {
                    smtp.Attachments.Add(source.Descriptor ?? new Definitions.FileAttachmentDescriptor(source.Path));
                }
            }

            await smtp.CreateMessageAsync(cancellationToken).ConfigureAwait(false);
            LogCollector.LogVerbose("Send-EmailMessage - Sending via SMTP fallback after Graph failure.");
            return await smtp.SendAsync(cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - SMTP fallback failed: {ex.Message}");
            // Preserve original Graph failure, but add fallback context to Error for diagnostics
            var fallbackError = ex.ToString();
            var mergedError = string.IsNullOrWhiteSpace(current.Error)
                ? $"Graph failed; SMTP fallback error: {fallbackError}"
                : $"{current.Error} | SMTP fallback error: {fallbackError}";
            return new SmtpResult(current.Status, current.EmailAction, current.SentTo, current.SentFrom, current.Server, current.Port, current.TimeToExecute, current.Message, mergedError) {
                GraphError = current.GraphError,
                MessageId = current.MessageId,
                Queued = current.Queued
            };
        }
    }

    internal void ApplyBodyToSmtpFallback(Smtp smtp) {
        if (smtp == null) throw new ArgumentNullException(nameof(smtp));

        if (string.Equals(ContentType, "Text", StringComparison.OrdinalIgnoreCase)) {
            smtp.TextBody = HTML;
            smtp.HtmlBody = string.Empty;
        } else {
            smtp.HtmlBody = HTML;
            smtp.TextBody = string.Empty;
        }
    }

    private static string MapImportance(MessagePriority priority) {
        return priority switch {
            MessagePriority.High => "high",
            MessagePriority.Low => "low",
            _ => "normal"
        };
    }

    /// <summary>
    /// Releases resources used by the Graph client.
    /// </summary>
    public void Dispose() {
        _client.Dispose();
    }
}

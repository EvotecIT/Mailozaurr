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
    public class Graph : IDisposable {
        private readonly HttpClient _client;
        /// <summary>
        /// Maximum size of an attachment chunk when uploading large files (4 MB).
        /// </summary>
        public const int MaxChunkSize = 4 * 1024 * 1024;
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
        /// Value indicating whether the total size of the attachments is larger than 4MB.
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

    /// <summary>
    /// Converts the <see cref="Attachments"/> collection into <see cref="GraphAttachment"/> instances.
    /// </summary>
    public void CreateAttachments() {
        ConvertedAttachments.Clear();
        TotalAttachmentSizeBytes = 0;
        IsLargerAttachment = false;
        _inlineAttachmentSizeBytes = 0;
        _fileAttachmentCount = 0;
        if (Attachments != null && Attachments.Any()) {
            var fileAttachments = new List<string>();
            long fileTotalBytes = 0;
            long inMemoryTotalBytes = 0;

            // First pass: compute total size without loading file contents.
            foreach (var item in Attachments) {
                if (item is string path) {
                    if (!File.Exists(path)) {
                        LogMissingAttachmentWarning(path);
                        continue;
                    }
                    fileAttachments.Add(path);
                    try {
                        var length = new FileInfo(path).Length;
                        fileTotalBytes += length;
                        _fileAttachmentCount++;
                    } catch (Exception ex) {
                        LogCollector.LogError($"Send-EmailMessage - Failed to read attachment '{path}': {ex.Message}");
                    }
                } else if (item is GraphAttachment ga) {
                    ConvertedAttachments.Add(ga);
                    var size = EstimateAttachmentSize(ga);
                    inMemoryTotalBytes += size;
                }
            }

            _inlineAttachmentSizeBytes = inMemoryTotalBytes;
            TotalAttachmentSizeBytes = fileTotalBytes + inMemoryTotalBytes;
            IsLargerAttachment = TotalAttachmentSizeBytes > 4_000_000;

            // Only load file attachments into memory when they fit in a simple send payload.
            if (!IsLargerAttachment && fileAttachments.Count > 0) {
                foreach (var path in fileAttachments) {
                    ConvertedAttachments.Add(GraphAttachment.FromFile(path));
                }
            }

            if (_inlineAttachmentSizeBytes > 4_000_000) {
                LogCollector.LogWarning("Send-EmailMessage - Large in-memory attachments detected. Consider using file paths for large attachments to enable upload sessions.");
            }
        }
    }

    private static long EstimateTotalSize(IEnumerable<GraphAttachment> attachments) {
        long total = 0;
        foreach (var attachment in attachments) {
            total += EstimateAttachmentSize(attachment);
        }
        return total;
    }

    private static long EstimateAttachmentSize(GraphAttachment attachment) {
        if (string.IsNullOrWhiteSpace(attachment.ContentBytes)) {
            return 0;
        }
        var value = attachment.ContentBytes.Trim();
        if (value.Length == 0) {
            return 0;
        }
        var padding = 0;
        if (value.EndsWith("==", StringComparison.Ordinal)) {
            padding = 2;
        } else if (value.EndsWith("=", StringComparison.Ordinal)) {
            padding = 1;
        }
        var bytes = (long)value.Length * 3 / 4 - padding;
        return bytes < 0 ? 0 : bytes;
    }

    /// <summary>
    /// Builds the <see cref="GraphMessageContainer"/> object that represents the email.
    /// </summary>
    public void CreateMessage() {
        CreateAttachments();
        if (AutoEmbedImages) {
            var (html, paths) = HtmlUtils.ExtractLocalImagePaths(HTML);
            HTML = html;
            foreach (var p in paths) {
                var att = GraphAttachment.FromFile(p);
                att.IsInline = true;
                att.ContentId = Path.GetFileName(p);
                ConvertedAttachments.Add(att);
                var size = EstimateAttachmentSize(att);
                _inlineAttachmentSizeBytes += size;
                TotalAttachmentSizeBytes += size;
            }
        }
        if (From is null) {
            throw new InvalidOperationException("From address must be specified.");
        }
        // Note: The display name for the sender is controlled by Office 365 and may not reflect the value you provide here.
        // Office 365 will use the mailbox's configured display name for the sender, regardless of what is set in the payload.
        // Always use the email address for API calls and authentication.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MessageContainer = new GraphMessageContainer {
            Message = new GraphMessage {
                From = ConvertToGraphEmailAddress(From),
                To = ConvertToGraphEmailAddressUnique(To, seen),
                Cc = ConvertToGraphEmailAddressUnique(Cc, seen),
                Bcc = ConvertToGraphEmailAddressUnique(Bcc, seen),
                ReplyTo = string.IsNullOrWhiteSpace(ReplyTo)
                    ? null
                    : new List<GraphEmailAddress> { ConvertToGraphEmailAddress(ReplyTo)! },
                Subject = Subject,
                Body = new GraphContent { Content = HTML, Type = ContentType },
                Importance = MapImportance(Priority),
                IsDeliveryReceiptRequested = RequestDeliveryReceipt,
                IsReadReceiptRequested = RequestReadReceipt
            },
            SaveToSentItems = !DoNotSaveToSentItems
        };
        if (_inlineAttachmentSizeBytes > 4_000_000) {
            throw new InvalidOperationException("In-memory attachments exceed the 4MB Graph payload limit. Use file path attachments or reduce attachment size.");
        }
        if (!IsLargerAttachment && TotalAttachmentSizeBytes > 4_000_000 && _fileAttachmentCount > 0) {
            throw new InvalidOperationException("Total attachment payload exceeds the 4MB Graph limit after embedding images. Use file attachments or reduce attachment size.");
        }
        if (ConvertedAttachments.Count > 0) {
            MessageContainer.Message.Attachments = ConvertedAttachments;
        }
        if (Headers != null && Headers.Count > 0) {
            MessageContainer.Message.InternetMessageHeaders = Headers.Select(kvp => new GraphInternetMessageHeader { Name = kvp.Key, Value = kvp.Value }).ToList();
        }

        MessageJson = JsonSerializer.Serialize(MessageContainer, MailozaurrJsonContext.Default.GraphMessageContainer);
        //LoggingMessages.Logger.WriteVerbose(MessageJson);
    }

    /// <summary>
    /// Parses the provided credentials into client id, secret and tenant domain.
    /// </summary>
    /// <param name="Credentials">The credentials to parse.</param>
    public void Authenticate(ICredentials Credentials) {
        if (Credentials is null) {
            throw new ArgumentNullException(nameof(Credentials));
        }

        if (Credentials is not NetworkCredential networkCredential) {
            throw new ArgumentException(
                "Credentials must be of type NetworkCredential.",
                nameof(Credentials));
        }

        if (string.IsNullOrWhiteSpace(networkCredential.UserName)) {
            throw new ArgumentException(
                "Credential.UserName must be in the format 'clientid@directoryid'",
                nameof(Credentials));
        }

        var userSplit = networkCredential.UserName.Split('@');
        if (userSplit.Length != 2 || string.IsNullOrWhiteSpace(userSplit[0]) || string.IsNullOrWhiteSpace(userSplit[1])) {
            throw new ArgumentException(
                "Credential.UserName must be in the format 'clientid@directoryid'",
                nameof(Credentials));
        }

        ApplicationID = userSplit[0];
        ApplicationKey = networkCredential.Password;
        TenantDomain = userSplit[1];
    }

    private GraphEmailAddress? ConvertToGraphEmailAddress(object? email) {
        if (email == null) {
            return null;
        }
        var address = Helpers.GetEmailAddress(email);
        return new GraphEmailAddress { Email = new GraphEmail { Address = address } };
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddress(object[]? emails) {
        if (emails == null) {
            return null;
        }
        return emails.Select(email => new GraphEmailAddress { Email = new GraphEmail { Address = Helpers.GetEmailAddress(email) } }).ToList();
    }

    private List<GraphEmailAddress>? ConvertToGraphEmailAddressUnique(object[]? emails, HashSet<string> seen) {
        if (emails == null) {
            return null;
        }

        var list = Helpers.UniqueAddresses(emails, seen)
            .Select(email => new GraphEmailAddress { Email = new GraphEmail { Address = Helpers.GetEmailAddress(email) } })
            .ToList();
        return list.Count == 0 ? null : list;
    }

    /// <summary>
    /// Authenticates to Microsoft Graph using client credentials and obtains an access token.
    /// </summary>
    /// <returns>The result of the connection attempt.</returns>
    public async Task<SmtpResult> ConnectO365GraphAsync(CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph authentication.");
            return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, "Connection skipped (WhatIf)");
        }
        string resource = "https://graph.microsoft.com";
        var body = new Dictionary<string, string> {
            { "grant_type", "client_credentials" },
            { "resource", resource },
            { "client_id", ApplicationID },
            { "client_secret", ApplicationKey }
        };

        try {
            await WaitForConcurrencyAsync(operationStopwatch, cancellationToken);
            try {
                using var requestContent = new FormUrlEncodedContent(body);
                using var response = await _client.PostAsync($"https://login.microsoftonline.com/{TenantDomain}/oauth2/token", requestContent, cancellationToken);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode) {
                    LogCollector.LogWarning($"Send-EmailMessage - Error during connection using Graph API: {content}");
                    if (ErrorAction == ActionPreference.Stop) {
                        response.EnsureSuccessStatusCode();
                    }
                    return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, content, content);
                }

                var authorization = JsonSerializer.Deserialize(content, MailozaurrJsonContext.Default.GraphAuthorization);
                AccessToken = authorization?.AccessToken ?? string.Empty;
                TokenType = authorization?.TokenType ?? string.Empty;
                return new SmtpResult(true, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, "", "");
            } finally {
                MicrosoftGraphUtils.ConcurrencySemaphore.Release();
            }
        } catch (TaskCanceledException ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Connection to Graph API cancelled: {ex.Message}");
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, ex.Message);
        } catch (Exception ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Error during connection using Graph API: {ex.Message}");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, EmailAction.Connect, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, ex.Message);
        }
    }

    /// <summary>
    /// Sends the prepared message via the Graph API.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendMessageAsync(CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        // create message
        CreateMessage();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        LogCollector.LogVerbose("Send-EmailMessage - Sending email via Graph API");
        // Create the request URI outside the loop.
        var requestUri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{MessageContainer.Message.From!.Email.Address}/sendMail");

        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        if (policy != null && policy.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policy.MaxConcurrency;
        }

        var policyDraft = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        if (policyDraft != null && policyDraft.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policyDraft.MaxConcurrency;
        }

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri) {
                    Content = new StringContent(MessageJson, Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

                await WaitForConcurrencyAsync(operationStopwatch, cancellationToken);
                try {
                    using var response = await _client.SendAsync(request, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode) {
                        var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, response.StatusCode.ToString(), "");
                        await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken);
                        return okResult;
                    }
                    var error = JsonSerializer.Deserialize(content, MailozaurrJsonContext.Default.GraphApiError);
                    var errorMessage = (error == null || error.Error == null || error.Error.InnerError == null)
                        ? $"Unknown error: {content}"
                        : $"Error code: {error.Error.Code}, message: {error.Error.Message}, request ID: {error.Error.InnerError.RequestId}, date: {error.Error.InnerError.Date}";
                    var retryAfter = ParseRetryAfter(response);
                    throw new GraphApiException(response.StatusCode, errorMessage, content, retryAfter);
                } finally {
                    MicrosoftGraphUtils.ConcurrencySemaphore.Release();
                }
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Sending via Graph API cancelled: {ex.Message}");
                var maxRetries = policy?.MaxRetries ?? RetryCount;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policy, failResult, ex, cancellationToken);
                }
                await DelayWithBackoffAsync(policy, attempts, null, ex, cancellationToken);
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
                var maxRetries = policy?.MaxRetries ?? RetryCount;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, "", ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policy, failResult, ex, cancellationToken);
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= (policy?.MaxRetries ?? RetryCount));

        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, "", lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return await TrySmtpFallbackAsync(policy, finalResult, lastException, cancellationToken);
    }

    /// <summary>
    /// Sends a message by first creating a draft and then uploading attachments.
    /// </summary>
    /// <returns>The result of the send operation.</returns>
    public async Task<SmtpResult> SendMessageDraftAsync(CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        if (DryRun) {
            CreateMessage();
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph draft send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        // Create the draft message using the new method
        var draftMessage = await CreateDraftMessageAsync(cancellationToken);

        // Upload attachments to the draft message
        await UploadAttachmentsAsync(draftMessage, cancellationToken);

        var policyDraft = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        if (policyDraft != null && policyDraft.MaxConcurrency > 0) {
            MicrosoftGraphUtils.MaxConcurrentRequests = policyDraft.MaxConcurrency;
        }

        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                return await SendDraftMessage(draftMessage, cancellationToken);
            } catch (TaskCanceledException ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Sending draft via Graph API cancelled: {ex.Message}");
                var maxRetries = policyDraft?.MaxRetries ?? RetryCount;
                var shouldRetry = (policyDraft?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policyDraft, failResult, ex, cancellationToken);
                }
                await DelayWithBackoffAsync(policyDraft, attempts, null, ex, cancellationToken);
            } catch (Exception ex) {
                lastException = ex;
                LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
                var maxRetries = policyDraft?.MaxRetries ?? RetryCount;
                var shouldRetry = (policyDraft?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, "", ex.Message);
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return await TrySmtpFallbackAsync(policyDraft, failResult, ex, cancellationToken);
                }
                var ra = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policyDraft, attempts, ra, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= (policyDraft?.MaxRetries ?? RetryCount));

        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, "", lastException?.Message);
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return await TrySmtpFallbackAsync(policyDraft, finalResult, lastException, cancellationToken);
    }

        /// <summary>
        /// Sends a previously created draft message.
        /// </summary>
        /// <param name="draftMessage">The draft message to send.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The result of the send operation.</returns>
        public async Task<SmtpResult> SendDraftMessage(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph draft send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        // Send the draft message
        var sendRequestUri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{MessageContainer.Message.From!.Email.Address}/messages/{draftMessage.Id!}/send");
        using var sendRequest = new HttpRequestMessage(HttpMethod.Post, sendRequestUri);

        // Add the authorization header
        sendRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for sending the draft message
        await WaitForConcurrencyAsync(operationStopwatch, cancellationToken);
        try {
            using var sendResponse = await _client.SendAsync(sendRequest, cancellationToken);

            // If the status code indicates success, return a successful result
            if (sendResponse.IsSuccessStatusCode) {
                var okResult = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, sendResponse.StatusCode.ToString(), "");
                await Helpers.PostWebhookAsync(WebhookUrl, okResult, cancellationToken);
                return okResult;
            }

            // If the status code indicates an error, throw an exception with the content
            var sendContent = await sendResponse.Content.ReadAsStringAsync();
            var sendError = JsonSerializer.Deserialize(sendContent, MailozaurrJsonContext.Default.GraphApiError);
            var sendErrorMessage = (sendError == null || sendError.Error == null || sendError.Error.InnerError == null)
                ? $"Unknown error: {sendContent}"
                : $"Error code: {sendError.Error.Code}, message: {sendError.Error.Message}, request ID: {sendError.Error.InnerError.RequestId}, date: {sendError.Error.InnerError.Date}";
            var retryAfter = ParseRetryAfter(sendResponse);
            throw new GraphApiException(sendResponse.StatusCode, sendErrorMessage, sendContent, retryAfter);
        } catch (GraphApiException ex) {
            LogCollector.LogWarning($"Send-EmailMessage - Error during sending using Graph API: {ex.Message}");
            var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, ex.ResponseContent, ex.Message);
            await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
            throw;
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }
    }

    /// <summary>
    /// Sends the current message using Microsoft Graph batch requests.
    /// </summary>
    public async Task<SmtpResult> SendMessageBatchAsync(CancellationToken cancellationToken = default) {
        var operationStopwatch = StartOperationTimer();
        CreateMessage();
        if (DryRun) {
            LogCollector.LogVerbose("Send-EmailMessage - DryRun enabled, skipping Graph batch send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, "GraphAPI", 0, operationStopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)");
        }
        var credential = new GraphCredential {
            ClientId = ApplicationID,
            ClientSecret = ApplicationKey,
            DirectoryId = TenantDomain
        };
        var bodyObj = JsonSerializer.Deserialize(MessageJson, MailozaurrJsonContext.Default.JsonElement);
        var request = new GraphBatchRequest {
            Id = "1",
            Method = GraphHttpMethod.POST,
            Url = $"/users/{MessageContainer.Message.From!.Email.Address}/sendMail",
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            Body = bodyObj
        };
        var results = await MicrosoftGraphUtils.SendBatchAsync(credential, new[] { request });
        var response = results.FirstOrDefault();
        var success = response != null && response.Status >= 200 && response.Status < 300;
        return new SmtpResult(
            success,
            EmailAction.Send,
            SentTo,
            SentFrom,
            "GraphAPI",
            0,
            operationStopwatch.Elapsed,
            response?.Status.ToString() ?? string.Empty,
            success ? string.Empty : response?.Body.ToString());
    }

    /// <summary>
    /// Creates a draft message on the server and returns the resulting <see cref="GraphMessage"/>.
    /// </summary>
    /// <returns>The created draft message.</returns>
    public async Task<GraphMessage> CreateDraftMessageAsync(CancellationToken cancellationToken = default) {
        // Create the draft message
        CreateMessage();

        //var options = new JsonSerializerOptions() {
        //    WriteIndented = true
        //};

        //// Serialize only the GraphMessage to a JSON string, excluding the SaveToSentItems property
        //var messageJson = JsonSerializer.Serialize(MessageContainer.Message, options);

        var messageJson = CreateDraft();

        var draftRequestUri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{MessageContainer.Message.From!.Email.Address}/mailfolders/drafts/messages");
        using var draftRequest = new HttpRequestMessage(HttpMethod.Post, draftRequestUri) {
            Content = new StringContent(messageJson, Encoding.UTF8, "application/json")
        };

        // Add the authorization header
        draftRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TokenType, AccessToken);

        // Send the HTTP request for creating the draft message
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        HttpResponseMessage draftResponse;
        try {
            draftResponse = await _client.SendAsync(draftRequest, cancellationToken);
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }

        using (draftResponse) {
            // Read the response content
            var draftContent = await draftResponse.Content.ReadAsStringAsync();

            if (!draftResponse.IsSuccessStatusCode) {
                var error = JsonSerializer.Deserialize(draftContent, MailozaurrJsonContext.Default.GraphApiError);
                var errorMessage = (error == null || error.Error == null)
                    ? $"Unknown error: {draftContent}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
                var retryAfter = ParseRetryAfter(draftResponse);
                throw new GraphApiException(draftResponse.StatusCode, errorMessage, draftContent, retryAfter);
            }

            // Deserialize the draft message
            var draftMessage = JsonSerializer.Deserialize(draftContent, MailozaurrJsonContext.Default.GraphMessage);

            if (draftMessage == null) {
                throw new InvalidOperationException("Failed to create draft message.");
            }

            return draftMessage;
        }
    }

    /// <summary>
    /// Creates a draft message locally and returns its JSON representation.
    /// </summary>
    /// <returns>The JSON payload for the draft message.</returns>
    public string CreateDraftForMg() {
        // Create the draft message
        CreateMessage();
        var messageJson = CreateDraft();
        return messageJson;
    }

    /// <summary>
    /// Serializes the current message to JSON without saving it to the Sent Items folder.
    /// </summary>
    /// <returns>The JSON representation of the message.</returns>
    public string CreateDraft() {
        CreateMessage();

        // Serialize only the GraphMessage to a JSON string, excluding the SaveToSentItems property
        var messageJson = JsonSerializer.Serialize(MessageContainer.Message, MailozaurrJsonContext.Default.GraphMessage);
        return messageJson;
    }


        /// <summary>
        /// Creates the metadata and content placeholders required for uploading a file attachment.
        /// </summary>
        /// <param name="attachmentPath">Path to the attachment file.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <param name="preloadContent">
        /// When true, loads file chunks into memory and populates <see cref="GraphAttachmentPlaceHolder.Content"/>.
        /// When false, only metadata is prepared and chunk content is generated on demand.
        /// </param>
        /// <returns>The placeholder representing the attachment.</returns>
        public Task<GraphAttachmentPlaceHolder> CreateGraphAttachment(string attachmentPath, CancellationToken cancellationToken = default, bool preloadContent = true) {
        if (!File.Exists(attachmentPath)) {
            LogMissingAttachmentWarning(attachmentPath);
            throw new FileNotFoundException($"Send-EmailMessage - Attachment file not found: {attachmentPath}", attachmentPath);
        }
        var fileName = Path.GetFileName(attachmentPath);
        var fileSize = new FileInfo(attachmentPath).Length;

        var attachmentItem = new GraphAttachmentItem("file", fileName, fileSize);

        var attachmentItemWrapper = new GraphAttachmentItemWrapper(attachmentItem);
        var attachmentItemJson = JsonSerializer.Serialize(attachmentItemWrapper, MailozaurrJsonContext.Default.GraphAttachmentItemWrapper);

        List<StreamContent> content = preloadContent
            ? PrepareByteArrayContentForUpload(attachmentPath, ChunkSize, cancellationToken)
            : new List<StreamContent>();

        var placeholder = new GraphAttachmentPlaceHolder {
            Json = attachmentItemJson,
            Content = content,
            FilePath = attachmentPath,
            FileSize = fileSize,
            FileName = fileName
        };

        return Task.FromResult(placeholder);
    }

        /// <summary>
        /// Creates an upload session for a large attachment.
        /// </summary>
        /// <param name="draftMessage">The draft message the attachment belongs to.</param>
        /// <param name="attachmentItemJson">The serialized attachment item.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The upload session URL.</returns>
        public async Task<string> CreateUploadSession(GraphMessage draftMessage, string attachmentItemJson, CancellationToken cancellationToken = default) {
        var uploadSessionUrl = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users('{SentFrom}')/messages/{draftMessage.Id}/attachments/createUploadSession");
        using var request = new HttpRequestMessage(HttpMethod.Post, uploadSessionUrl) {
            Content = new StringContent(attachmentItemJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TokenType, AccessToken);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        HttpResponseMessage uploadSessionResponse;
        try {
            uploadSessionResponse = await _client.SendAsync(request, cancellationToken);
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }

        using (uploadSessionResponse) {
            var uploadSessionContent = await uploadSessionResponse.Content.ReadAsStringAsync();
            if (!uploadSessionResponse.IsSuccessStatusCode) {
                GraphApiError? error = null;
                try {
                    error = JsonSerializer.Deserialize(uploadSessionContent, MailozaurrJsonContext.Default.GraphApiError);
                } catch (JsonException) {
                    // Non-JSON error response; fall back to raw content.
                }
                var errorMessage = (error == null || error.Error == null)
                    ? $"Unknown error: {uploadSessionContent}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
                var retryAfter = ParseRetryAfter(uploadSessionResponse);
                throw new GraphApiException(uploadSessionResponse.StatusCode, errorMessage, uploadSessionContent, retryAfter);
            }

            return ParseUploadSessionResult(uploadSessionContent);
        }
    }

    private static string ParseUploadSessionResult(string uploadSessionContent) {
        var uploadSessionResult = JsonSerializer.Deserialize(uploadSessionContent, MailozaurrJsonContext.Default.GraphUploadSessionResult)
            ?? throw new InvalidOperationException("Failed to deserialize the upload session response.");

        if (string.IsNullOrEmpty(uploadSessionResult.UploadUrl)) {
            throw new InvalidOperationException("Upload URL not found in the session response.");
        }

        return uploadSessionResult.UploadUrl;
    }

        /// <summary>
        /// Splits <paramref name="filePath"/> into chunks no larger than <see cref="MaxChunkSize"/>.
        /// </summary>
        /// <param name="filePath">Path to the file to split.</param>
        /// <param name="chunkSize">Desired size of each chunk in bytes.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>List of stream contents representing file chunks.</returns>
        private List<StreamContent> PrepareByteArrayContentForUpload(string filePath, int chunkSize = MaxChunkSize, CancellationToken cancellationToken = default) {
        chunkSize = Math.Min(chunkSize, MaxChunkSize);
        var fileContents = new List<StreamContent>();
        var fileSize = new FileInfo(filePath).Length;

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
        int bytesRead;
        long offset = 0;
        try {
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0) {
                if (cancellationToken.IsCancellationRequested) {
                    return fileContents;
                }

                var chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                var memoryStream = new MemoryStream(chunk, writable: false);
                var contentRange = $"bytes {offset}-{offset + bytesRead - 1}/{fileSize}";
                var streamContent = new StreamContent(memoryStream);
                streamContent.Headers.Add("Content-Range", contentRange);
                fileContents.Add(streamContent);
                offset += bytesRead;
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        return fileContents;
    }

        /// <summary>
        /// Uploads all attachments for the specified draft message.
        /// </summary>
        /// <param name="draftMessage">The draft message to attach the files to.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async Task UploadAttachmentsAsync(GraphMessage draftMessage, CancellationToken cancellationToken = default) {
        if (Attachments != null && Attachments.Length > 0) {
            foreach (var path in Attachments.OfType<string>()) {
                try {
                    await UploadAttachmentWithRetryAsync(draftMessage, path, cancellationToken);
                } catch (FileNotFoundException) {
                    // Already logged by CreateGraphAttachment.
                }
            }
        }
    }

        /// <summary>
        /// Prepares attachments for upload by creating placeholders.
        /// </summary>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async Task PrepareAttachments(CancellationToken cancellationToken = default) {
        if (Attachments != null && Attachments.Length > 0) {
            foreach (var path in Attachments.OfType<string>()) {
                try {
                    var attachmentItemJson = await CreateGraphAttachment(path, cancellationToken);
                    AttachmentsPlaceHolders.Add(attachmentItemJson);
                } catch (FileNotFoundException) {
                    // Already logged by CreateGraphAttachment.
                }
            }
        }
    }

        /// <summary>
        /// Uploads all chunks of a file to the provided upload session URL.
        /// </summary>
        /// <param name="uploadUrl">The upload session URL.</param>
        /// <param name="fileChunks">The file chunks to upload.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async Task SendFileChunks(string uploadUrl, IEnumerable<StreamContent> fileChunks, CancellationToken cancellationToken = default) {
        foreach (var chunk in fileChunks) {
            await SendAttachmentChunk(uploadUrl, chunk, cancellationToken);
        }
    }

        /// <summary>
        /// Uploads all chunks of a file to the provided upload session URL without buffering the entire file.
        /// </summary>
        public async Task SendFileChunks(string uploadUrl, string filePath, long fileSize, CancellationToken cancellationToken = default) {
        var chunkSize = Math.Min(ChunkSize, MaxChunkSize);
        var buffer = new byte[chunkSize];
        long offset = 0;
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        int bytesRead;
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
            var chunk = new byte[bytesRead];
            Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
            await SendAttachmentChunkWithRetryAsync(uploadUrl, chunk, offset, fileSize, cancellationToken);
            offset += bytesRead;
        }
    }

        /// <summary>
        /// Uploads a single attachment chunk to the Graph API.
        /// </summary>
        /// <param name="uploadUrl">The upload session URL.</param>
        /// <param name="byteArrayContent">The chunk to send.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async Task SendAttachmentChunk(string uploadUrl, StreamContent byteArrayContent, CancellationToken cancellationToken = default) {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, uploadUrl) {
            Content = byteArrayContent
        };
        requestMessage.Headers.Add("AnchorMailbox", SentFrom);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        try {
            using var uploadChunkResponse = await _client.SendAsync(requestMessage, cancellationToken);
            if (!uploadChunkResponse.IsSuccessStatusCode) {
                LogCollector.LogWarning(uploadChunkResponse.ToString());
            }
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }
    }

    private async Task SendAttachmentChunkWithRetryAsync(string uploadUrl, byte[] chunk, long offset, long fileSize, CancellationToken cancellationToken) {
        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        int attempts = 0;
        Exception? lastException = null;
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        do {
            try {
                await SendAttachmentChunkOnceAsync(uploadUrl, chunk, offset, fileSize, cancellationToken);
                return;
            } catch (Exception ex) {
                lastException = ex;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= maxRetries);

        if (lastException != null) {
            throw lastException;
        }
    }

    private async Task UploadAttachmentWithRetryAsync(GraphMessage draftMessage, string path, CancellationToken cancellationToken) {
        var policy = SendPolicy ?? MailozaurrOptions.DefaultGraphPolicy;
        int attempts = 0;
        Exception? lastException = null;
        var maxRetries = policy?.MaxRetries ?? RetryCount;
        do {
            try {
                var attachmentItemJson = await CreateGraphAttachment(path, cancellationToken, preloadContent: false);
                var uploadUrl = await CreateUploadSession(draftMessage, attachmentItemJson.Json, cancellationToken);
                await SendFileChunks(uploadUrl, attachmentItemJson.FilePath, attachmentItemJson.FileSize, cancellationToken);
                return;
            } catch (FileNotFoundException) {
                throw;
            } catch (Exception ex) {
                lastException = ex;
                var shouldRetry = (policy?.RetryOnTransient ?? true) ? GraphRetryHelper.IsTransient(ex) : RetryAlways;
                if ((!shouldRetry && !RetryAlways) || attempts >= maxRetries) {
                    throw;
                }
                var retryAfter = (ex as GraphApiException)?.RetryAfter;
                await DelayWithBackoffAsync(policy, attempts, retryAfter, ex, cancellationToken);
            }
            attempts++;
        } while (attempts <= maxRetries);

        if (lastException != null) {
            throw lastException;
        }
    }

    private async Task SendAttachmentChunkOnceAsync(string uploadUrl, byte[] chunk, long offset, long fileSize, CancellationToken cancellationToken) {
        using var content = new StreamContent(new MemoryStream(chunk, writable: false));
        var contentRange = $"bytes {offset}-{offset + chunk.Length - 1}/{fileSize}";
        content.Headers.Add("Content-Range", contentRange);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, uploadUrl) {
            Content = content
        };
        requestMessage.Headers.Add("AnchorMailbox", SentFrom);
        await MicrosoftGraphUtils.ConcurrencySemaphore.WaitAsync(cancellationToken);
        try {
            using var uploadChunkResponse = await _client.SendAsync(requestMessage, cancellationToken);
            if (!uploadChunkResponse.IsSuccessStatusCode) {
                var responseContent = await uploadChunkResponse.Content.ReadAsStringAsync();
                GraphApiError? error = null;
                try {
                    error = JsonSerializer.Deserialize(responseContent, MailozaurrJsonContext.Default.GraphApiError);
                } catch (JsonException) {
                    // Non-JSON error response; fall back to raw content.
                }
                var errorMessage = (error == null || error.Error == null)
                    ? $"Unknown error: {responseContent}"
                    : $"Error code: {error.Error.Code}, message: {error.Error.Message}";
                var retryAfter = ParseRetryAfter(uploadChunkResponse);
                throw new GraphApiException(uploadChunkResponse.StatusCode, errorMessage, responseContent, retryAfter);
            }
        } finally {
            MicrosoftGraphUtils.ConcurrencySemaphore.Release();
        }
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response) {
        if (response.Headers.TryGetValues("Retry-After", out var values)) {
            var first = values.FirstOrDefault();
            if (int.TryParse(first, out var seconds)) {
                return TimeSpan.FromSeconds(Math.Max(0, seconds));
            }
            if (DateTimeOffset.TryParse(first, out var ts)) {
                var delta = ts - DateTimeOffset.UtcNow;
                return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
            }
        }
        return null;
    }

    private async Task DelayWithBackoffAsync(GraphSendPolicy? policy, int attempts, TimeSpan? retryAfter, Exception ex, CancellationToken cancellationToken) {
        TimeSpan delay = TimeSpan.Zero;
        if (policy != null) {
            delay = GraphRetryHelper.CalculateDelay(policy, attempts);
            if (GraphRetryHelper.IsThrottled(ex) && retryAfter.HasValue && retryAfter.Value > delay) {
                delay = retryAfter.Value;
            }
            if (policy.MaxDelayMs > 0 && delay > TimeSpan.FromMilliseconds(policy.MaxDelayMs)) {
                delay = TimeSpan.FromMilliseconds(policy.MaxDelayMs);
            }
        } else {
            var delayMs = (int)Math.Round(RetryDelayMilliseconds * Math.Pow(RetryDelayBackoff, attempts));
            if (delayMs > 0) delay = TimeSpan.FromMilliseconds(delayMs);
        }

        if (delay > TimeSpan.Zero) {
            var reason = GraphRetryHelper.IsThrottled(ex) ? "throttling" : "transient";
            LogCollector.LogVerbose($"Send-EmailMessage - Retry attempt {attempts + 1}, delaying {delay.TotalMilliseconds:N0} ms due to {reason}.");
            await Task.Delay(delay, cancellationToken);
        }
    }

    private const string DefaultAttachmentName = "attachment.bin";

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
            smtp.HtmlBody = this.HTML;
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
            return new SmtpResult(current.Status, current.EmailAction, current.SentTo, current.SentFrom, current.Server, current.Port, current.TimeToExecute, current.Message, mergedError)
            {
                GraphError = current.GraphError,
                MessageId = current.MessageId
            };
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

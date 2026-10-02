using Mailozaurr.Definitions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public partial class Smtp {
    /// <summary>
    /// Send the email message.
    /// </summary>
    /// <remarks>
    /// Concurrent calls are serialized so that only one send executes at a time for a
    /// given instance.
    /// </remarks>
    /// <returns></returns>
    public SmtpResult Send() {
        _sendLock.Wait();
        try {
            return SendCoreAsync().GetAwaiter().GetResult();
        } finally {
            AttachmentDescriptorLifetime.ReleaseStaging(Attachments, InlineAttachments);
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Send the email message asynchronously.
    /// </summary>
    /// <remarks>
    /// Concurrent calls are serialized so that only one send executes at a time for a
    /// given instance.
    /// </remarks>
    /// <returns></returns>
    public async Task<SmtpResult> SendAsync(CancellationToken cancellationToken = default) {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            return await SendCoreAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            AttachmentDescriptorLifetime.ReleaseStaging(Attachments, InlineAttachments);
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Attempts to send all messages stored in <see cref="PendingMessageRepository"/>.
    /// </summary>
    /// <remarks>
    /// Messages are removed from the repository only when sending succeeds. On
    /// success the message is also logged via <see cref="SentMessageRepository"/>,
    /// if configured.
    /// </remarks>
    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default) {
        if (PendingMessageRepository == null || DryRun) return;
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            var factory = new PendingMessageSenderFactory(new[] {
                new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.None, new ConfiguredPendingSmtpSender(this))
            });
            var processor = new PendingMessageProcessor(PendingMessageRepository, factory,
                retryDelaySelector: CalculateRetryDelay, maxRetryAttempts: int.MaxValue,
                permanentFailureDetector: _ => false) { ProviderFilter = EmailProvider.None };
            await processor.ProcessAsync(cancellationToken).ConfigureAwait(false);
        } finally {
            _sendLock.Release();
        }
    }
    /// <summary>
    /// Logs a verbose message using LogCollector if available, otherwise uses LoggingMessages.Logger.
    /// </summary>
    private void LogVerbose(string message) {
        if (LogCollector != null) {
            LogCollector.LogVerbose(message);
        } else {
            LoggingMessages.Logger.WriteVerbose(message);
        }
    }

    /// <summary>
    /// Logs a warning message using LogCollector if available, otherwise uses LoggingMessages.Logger.
    /// </summary>
    private void LogWarning(string message) {
        if (LogCollector != null) {
            LogCollector.LogWarning(message);
        } else {
            LoggingMessages.Logger.WriteWarning(message);
        }
    }

    private Dictionary<string, string> CreateProviderDataSnapshot() {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            [ProviderDataSecureSocketOptionsKey] = _activeSecureSocketOptions.ToString(),
            [ProviderDataUseSslKey] = _activeUseSsl.ToString(CultureInfo.InvariantCulture),
            [ProviderDataSkipCertificateValidationKey] = _skipCertificateValidation.ToString(CultureInfo.InvariantCulture),
            [ProviderDataCheckCertificateRevocationKey] = Client.CheckCertificateRevocation.ToString(CultureInfo.InvariantCulture),
            [ProviderDataTimeoutKey] = Client.Timeout.ToString(CultureInfo.InvariantCulture)
        };

        return data;
    }

    private string GetConnectionPoolIdentity() {
        return GetPoolUserIdentity(Credential) + "|" + GetPoolTransportPolicy();
    }

    private string GetPoolUserIdentity(System.Net.NetworkCredential? credential) {
        var userName = credential?.UserName;
        var domain = credential?.Domain ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(domain)) {
            userName = string.IsNullOrWhiteSpace(userName) ? domain : $"{domain}\\{userName}";
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            userName = string.IsNullOrWhiteSpace(ConnectionPoolIdentity) ? "anonymous" : ConnectionPoolIdentity;
        } else if (!string.IsNullOrWhiteSpace(ConnectionPoolIdentity) && !string.Equals(ConnectionPoolIdentity, userName, StringComparison.Ordinal)) {
            userName = $"{ConnectionPoolIdentity}|{userName}";
        }
        return (credential == null ? "unauthenticated:" : "authenticated:") + userName;
    }

    private string GetPoolTransportPolicy() {
        var callback = Client.ServerCertificateValidationCallback;
        var callbackIdentity = callback == null ? 0 : CallbackIdentities.GetValue(callback, _ => new CallbackIdentity()).Value;
        return $"{_activeSecureSocketOptions}|{_activeUseSsl}|{Client.CheckCertificateRevocation}|{callbackIdentity}|{Client.LocalDomain}";
    }

    internal TimeSpan CalculateRetryDelay(int attempt) =>
        RetryDelayCalculator.Calculate(
            RetryDelayMilliseconds,
            RetryDelayBackoff,
            attempt,
            MaxDelayMilliseconds,
            JitterMilliseconds);

    private string EnsureMessageId() {
        var id = Message.MessageId;
        if (string.IsNullOrEmpty(id)) {
            Message.MessageId = id = MimeKit.Utils.MimeUtils.GenerateMessageId();
        }
        return id!;
    }

    private async Task SaveSentMessageAsync(string messageId, CancellationToken cancellationToken) {
        if (SentMessageRepository == null) {
            return;
        }

        var record = new SentMessageRecord {
            MessageId = messageId,
            Recipients = SentMessageRecipients.Serialize(Message?.To),
            Subject = Subject,
            Timestamp = DateTimeOffset.UtcNow
        };
        await SentMessageRepository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemovePendingMessageAsync(string? messageId, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null || string.IsNullOrEmpty(messageId)) {
            return;
        }

        var safeMessageId = messageId!;
        await PendingMessageRepository.RemoveAsync(safeMessageId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> EnqueuePendingMessageAsync(string messageId, ICredentialProtector credentialProtector, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return false;
        }

        using var ms = new MemoryStream();
        await Message.WriteToAsync(ms, cancellationToken).ConfigureAwait(false);
        var record = new PendingMessageRecord {
            MessageId = messageId,
            MimeMessage = Convert.ToBase64String(ms.ToArray()),
            Timestamp = DateTimeOffset.UtcNow,
            NextAttemptAt = DateTimeOffset.UtcNow,
            Provider = EmailProvider.None,
            Server = Server,
            Port = Port,
            UserName = Credential?.UserName,
            Password = string.IsNullOrEmpty(Credential?.Password)
                ? null
                : credentialProtector.Protect(Credential!.Password),
            ProviderData = CreateProviderDataSnapshot()
        };
        await PendingMessageRepository.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<SmtpResult?> EnsureMessageReadyAsync(CancellationToken cancellationToken) {
        var message = Message;
        bool messageHasContent = message != null && MessageHasContent(message);
        bool hasPayload = HasPropertyPayload();
        bool hasHeaderPayload = (message != null && message.Headers != null && message.Headers.Count > 0) ||
                                (Headers != null && Headers.Count > 0);

        bool shouldAutoCreate = AutoCreateMessage && !messageHasContent && hasPayload;
        if (shouldAutoCreate) {
            PreserveCustomHeaders(message);
            await CreateMessageAsync(cancellationToken).ConfigureAwait(false);
            message = Message;
            messageHasContent = message != null && MessageHasContent(message);
        }

        bool hasSender = message != null && MessageHasSender(message);
        bool hasMaterial = hasPayload || messageHasContent || hasHeaderPayload;
        if (!hasSender && hasMaterial) {
            string messageText = "SMTP message has no sender. Call CreateMessage/CreateMessageAsync after setting From/To/Subject, or enable AutoCreateMessage.";
            LogWarning($"Send-EmailMessage - {messageText}");
            if (ErrorAction == ActionPreference.Stop) {
                throw new InvalidOperationException(messageText);
            }

            var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", messageText) {
                MessageId = Message?.MessageId
            };
            await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken).ConfigureAwait(false);
            return failResult;
        }

        return null;
    }

    private bool HasPropertyPayload() {
        if (HasAddressValue(From) || HasAddressValue(ReplyTo)) {
            return true;
        }

        if (HasRecipientValues(To) || HasRecipientValues(Cc) || HasRecipientValues(Bcc)) {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(Subject) ||
            !string.IsNullOrWhiteSpace(HtmlBody) ||
            !string.IsNullOrWhiteSpace(TextBody)) {
            return true;
        }

        if (Attachments != null && Attachments.Count > 0) {
            return true;
        }

        if (InlineAttachments != null && InlineAttachments.Count > 0) {
            return true;
        }

        if (Headers != null && Headers.Count > 0) {
            return true;
        }

        return false;
    }

    private static bool MessageHasSender(MimeMessage message) {
        if (message == null) {
            return false;
        }

        if (message.From.Count > 0) {
            return true;
        }

        return message.Sender != null;
    }

    private static bool MessageHasContent(MimeMessage message) {
        if (message == null) {
            return false;
        }

        if (message.From.Count > 0 || message.To.Count > 0 || message.Cc.Count > 0 || message.Bcc.Count > 0 ||
            message.ReplyTo.Count > 0 || message.Sender != null) {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(message.Subject)) {
            return true;
        }

        return message.Body != null;
    }

    private void PreserveCustomHeaders(MimeMessage? message) {
        if (message == null || message.Headers == null || message.Headers.Count == 0) {
            return;
        }

        Dictionary<string, string>? merged = null;
        if (Headers is Dictionary<string, string> headerDict) {
            merged = headerDict;
        } else if (Headers != null && Headers.Count > 0) {
            merged = new Dictionary<string, string>(Headers, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var header in message.Headers) {
            if (header.Id != MimeKit.HeaderId.Unknown) {
                continue;
            }

            if (string.IsNullOrWhiteSpace(header.Field)) {
                continue;
            }

            merged ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!merged.ContainsKey(header.Field)) {
                merged[header.Field] = header.Value ?? string.Empty;
            }
        }

        if (merged != null && !ReferenceEquals(merged, Headers)) {
            Headers = merged;
        }
    }

    private static bool HasAddressValue(object? value) {
        if (value == null) {
            return false;
        }

        if (value is string text) {
            return !string.IsNullOrWhiteSpace(text);
        }

        return true;
    }

    private static bool HasRecipientValues(IEnumerable<object>? recipients) {
        if (recipients == null) {
            return false;
        }

        foreach (var recipient in recipients) {
            if (recipient == null) {
                continue;
            }

            if (recipient is string text) {
                if (!string.IsNullOrWhiteSpace(text)) {
                    return true;
                }

                continue;
            }

            return true;
        }

        return false;
    }

    private async Task<SmtpResult> SendCoreAsync(CancellationToken cancellationToken = default) {
        if (DryRun) {
            LogVerbose("Send-EmailMessage - DryRun enabled, skipping send.");
            return new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)") {
                MessageId = Message?.MessageId
            };
        }
        var readinessResult = await EnsureMessageReadyAsync(cancellationToken).ConfigureAwait(false);
        if (readinessResult != null) return readinessResult;
        var messageId = EnsureMessageId();
        MarkTransportAttempted();
        for (var attempt = 0; ; attempt++) {
            try {
                await Client.SendAsync(Message, cancellationToken).ConfigureAwait(false);
                break;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception exception) {
                LogWarning($"Send-EmailMessage - Error during sending: {exception.Message}");
                if ((!Helpers.IsTransient(exception) && !RetryAlways) || attempt >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) throw;
                    var queued = await EnqueuePendingMessageAsync(messageId, CredentialProtection.Default, cancellationToken).ConfigureAwait(false);
                    var failure = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", exception.Message) {
                        MessageId = messageId, Queued = queued
                    };
                    await RecordPostSendErrorAsync(failure, "Webhook notification", () => Helpers.PostWebhookAsync(WebhookUrl, failure, cancellationToken)).ConfigureAwait(false);
                    return failure;
                }
                var delay = CalculateRetryDelay(attempt);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        var result = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging) {
            MessageId = messageId, DeliveryAccepted = true
        };
        LogVerbose($"Send-EmailMessage - Sent email to {SentTo}");
        return await CompleteAcceptedSendAsync(result, cancellationToken).ConfigureAwait(false);
    }
}

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
        await _sendLock.WaitAsync(cancellationToken);
        try {
            return await SendCoreAsync(cancellationToken);
        } finally {
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
        if (PendingMessageRepository == null) {
            return;
        }

        await foreach (var record in PendingMessageRepository.GetAllAsync(cancellationToken)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (DryRun) {
                LogVerbose($"ProcessPendingMessages - DryRun enabled, skipping {record.MessageId}");
                continue;
            }
            if (string.IsNullOrWhiteSpace(record.MimeMessage) || string.IsNullOrEmpty(record.MessageId)) {
                continue;
            }
            if (record.NextAttemptAt > DateTimeOffset.UtcNow) {
                continue;
            }

            if (record.Provider != EmailProvider.None) {
                continue;
            }

            MimeMessage message;
            try {
                var bytes = Convert.FromBase64String(record.MimeMessage);
                using var ms = new MemoryStream(bytes);
                message = await MimeMessage.LoadAsync(ms, cancellationToken);
            } catch (Exception ex) {
                LogWarning($"ProcessPendingMessages - Failed to parse {record.MessageId}: {ex.Message}");
                continue;
            }

            var originalSkipValidation = SkipCertificateValidation;
            var originalCheckRevocation = CheckCertificateRevocation;
            var originalTimeout = Timeout;
            var originalSecureOptions = _activeSecureSocketOptions;
            var originalUseSsl = _activeUseSsl;
            var originalPoolIdentity = ConnectionPoolIdentity;
            var secureSocketOptions = _activeSecureSocketOptions;
            var useSsl = _activeUseSsl;
            if (record.ProviderData != null && record.ProviderData.Count > 0) {
                if (record.ProviderData.TryGetValue(ProviderDataSecureSocketOptionsKey, out var secureValue)
                    && Enum.TryParse(secureValue, out SecureSocketOptions parsedSecure)) {
                    secureSocketOptions = parsedSecure;
                }
                if (record.ProviderData.TryGetValue(ProviderDataUseSslKey, out var useSslValue)
                    && bool.TryParse(useSslValue, out var parsedUseSsl)) {
                    useSsl = parsedUseSsl;
                }
                if (record.ProviderData.TryGetValue(ProviderDataSkipCertificateValidationKey, out var skipValue)
                    && bool.TryParse(skipValue, out var parsedSkip)) {
                    SkipCertificateValidation = parsedSkip;
                }
                if (record.ProviderData.TryGetValue(ProviderDataCheckCertificateRevocationKey, out var revocationValue)
                    && bool.TryParse(revocationValue, out var parsedRevocation)) {
                    CheckCertificateRevocation = parsedRevocation;
                }
                if (record.ProviderData.TryGetValue(ProviderDataTimeoutKey, out var timeoutValue)
                    && int.TryParse(timeoutValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)) {
                    Timeout = timeout;
                }
            }
            if (!string.IsNullOrWhiteSpace(record.UserName)) {
                ConnectionPoolIdentity = record.UserName;
            }

            try {
                var server = record.Server ?? Server;
                var port = record.Port ?? Port;
                if (!string.IsNullOrWhiteSpace(server)) {
                    Connect(server, port, secureSocketOptions, useSsl);
                    if (!string.IsNullOrEmpty(record.UserName)) {
                        var pwd = CredentialProtection.UnprotectWithFallback(record.Password);
                        var cred = Helpers.ConvertFromPlainText(record.UserName!, pwd);
                        Authenticate(cred);
                    }
                }

                await Client.SendAsync(message, cancellationToken);
                LogVerbose($"Send-EmailMessage - Sent email to {message.To}");
                if (SentMessageRepository != null) {
                    var sentRecord = new SentMessageRecord {
                        MessageId = message.MessageId ?? record.MessageId,
                        Recipients = SentMessageRecipients.Serialize(message.To),
                        Subject = message.Subject ?? string.Empty,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    await SentMessageRepository.SaveAsync(sentRecord, cancellationToken);
                }
                await PendingMessageRepository.RemoveAsync(record.MessageId!, cancellationToken);
            } catch (Exception ex) {
                LogWarning($"ProcessPendingMessages - Error sending {record.MessageId}: {ex.Message}");
                var attempt = record.IncrementAttemptCount();
                var delay = CalculateRetryDelay(attempt - 1);
                record.NextAttemptAt = delay > TimeSpan.Zero
                    ? DateTimeOffset.UtcNow.Add(delay)
                    : DateTimeOffset.UtcNow;
                await PendingMessageRepository.SaveAsync(record, cancellationToken);
            } finally {
                Disconnect();
                SkipCertificateValidation = originalSkipValidation;
                CheckCertificateRevocation = originalCheckRevocation;
                Timeout = originalTimeout;
                _activeSecureSocketOptions = originalSecureOptions;
                _activeUseSsl = originalUseSsl;
                ConnectionPoolIdentity = originalPoolIdentity;
            }
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
        var userName = ConnectionPoolIdentity;
        var domain = string.Empty;
        if (string.IsNullOrWhiteSpace(userName)) {
            userName = Credential?.UserName;
            domain = Credential?.Domain ?? string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(domain)) {
            userName = string.IsNullOrWhiteSpace(userName) ? domain : $"{domain}\\{userName}";
        }
        if (string.IsNullOrWhiteSpace(userName)) {
            userName = "anonymous";
        }
        return $"{userName}|{_activeSecureSocketOptions}|{_activeUseSsl}";
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
        await SentMessageRepository.SaveAsync(record, cancellationToken);
    }

    private async Task RemovePendingMessageAsync(string? messageId, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null || string.IsNullOrEmpty(messageId)) {
            return;
        }

        var safeMessageId = messageId!;
        await PendingMessageRepository.RemoveAsync(safeMessageId, cancellationToken);
    }

    private async Task<bool> EnqueuePendingMessageAsync(string messageId, ICredentialProtector credentialProtector, CancellationToken cancellationToken) {
        if (PendingMessageRepository == null) {
            return false;
        }

        using var ms = new MemoryStream();
        await Message.WriteToAsync(ms, cancellationToken);
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
        await PendingMessageRepository.SaveAsync(record, cancellationToken);
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
        int attempts = 0;
        Exception? lastException = null;
        var credentialProtector = CredentialProtection.Default;
        var readinessResult = await EnsureMessageReadyAsync(cancellationToken).ConfigureAwait(false);
        if (readinessResult != null) {
            return readinessResult;
        }

        do {
            try {
                await Client.SendAsync(Message, cancellationToken);
                LogVerbose($"Send-EmailMessage - Sent email to {SentTo}");
                await SaveSentMessageAsync(Message.MessageId ?? string.Empty, cancellationToken);
                await RemovePendingMessageAsync(Message.MessageId, cancellationToken);
                var result = new SmtpResult(true, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, Logging) {
                    MessageId = Message.MessageId
                };
                await Helpers.PostWebhookAsync(WebhookUrl, result, cancellationToken);
                return result;
            } catch (Exception ex) {
                lastException = ex;
                LogWarning($"Send-EmailMessage - Error during sending: {ex.Message}");
                if ((!Helpers.IsTransient(ex) && !RetryAlways) || attempts >= RetryCount) {
                    if (ErrorAction == ActionPreference.Stop) {
                        throw;
                    }
                    var id = EnsureMessageId();
                    var queued = await EnqueuePendingMessageAsync(id, credentialProtector, cancellationToken);
                    var failResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", ex.Message) {
                        MessageId = id,
                        Queued = queued
                    };
                    await Helpers.PostWebhookAsync(WebhookUrl, failResult, cancellationToken);
                    return failResult;
                }

                var delay = CalculateRetryDelay(attempts);
                if (delay > TimeSpan.Zero) {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            attempts++;
        } while (attempts <= RetryCount);

        var finalId = EnsureMessageId();
        var finalQueued = await EnqueuePendingMessageAsync(finalId, credentialProtector, cancellationToken);
        var finalResult = new SmtpResult(false, EmailAction.Send, SentTo, SentFrom, Server, Port, Stopwatch.Elapsed, "", lastException?.Message) {
            MessageId = finalId,
            Queued = finalQueued
        };
        await Helpers.PostWebhookAsync(WebhookUrl, finalResult, cancellationToken);
        return finalResult;
    }
}

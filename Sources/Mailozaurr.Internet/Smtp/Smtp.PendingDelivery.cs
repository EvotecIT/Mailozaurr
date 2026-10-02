using System.Globalization;

namespace Mailozaurr;

public partial class Smtp {
    private sealed class ConfiguredPendingSmtpSender : IPendingMessageSender {
        private readonly Smtp smtp;
        internal ConfiguredPendingSmtpSender(Smtp smtp) => this.smtp = smtp;

        public async Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
            var bytes = Convert.FromBase64String(record.MimeMessage);
            using var stream = new MemoryStream(bytes);
            using var message = await MimeMessage.LoadAsync(stream, ct).ConfigureAwait(false);
            var originalSkip = smtp.SkipCertificateValidation;
            var originalCallback = smtp.ServerCertificateValidationCallback;
            var originalRevocation = smtp.CheckCertificateRevocation;
            var originalTimeout = smtp.Timeout;
            var originalSecureOptions = smtp._activeSecureSocketOptions;
            var originalUseSsl = smtp._activeUseSsl;
            var originalIdentity = smtp.ConnectionPoolIdentity;
            var originalCredential = smtp.Credential;
            try {
                var data = record.ProviderData;
                var options = originalSecureOptions;
                var useSsl = originalUseSsl;
                if (data != null) {
                    if (data.TryGetValue(ProviderDataSecureSocketOptionsKey, out var value) && Enum.TryParse(value, out SecureSocketOptions parsed)) options = parsed;
                    if (data.TryGetValue(ProviderDataUseSslKey, out value) && bool.TryParse(value, out var flag)) useSsl = flag;
                    if (data.TryGetValue(ProviderDataSkipCertificateValidationKey, out value) && bool.TryParse(value, out flag)) smtp.SkipCertificateValidation = flag;
                    if (data.TryGetValue(ProviderDataCheckCertificateRevocationKey, out value) && bool.TryParse(value, out flag)) smtp.CheckCertificateRevocation = flag;
                    if (data.TryGetValue(ProviderDataTimeoutKey, out value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)) smtp.Timeout = timeout;
                }
                if (!string.IsNullOrWhiteSpace(record.UserName)) smtp.ConnectionPoolIdentity = record.UserName;
                smtp.Credential = string.IsNullOrEmpty(record.UserName) ? null :
                    new NetworkCredential(record.UserName, CredentialProtection.UnprotectWithFallback(record.Password));
                var server = record.Server ?? smtp.Server;
                if (!string.IsNullOrWhiteSpace(server)) {
                    var connect = await smtp.ConnectAsync(server, record.Port ?? smtp.Port, options, useSsl, ct).ConfigureAwait(false);
                    if (!connect.Status) throw new InvalidOperationException(connect.Error ?? "SMTP connection failed.");
                    if (!string.IsNullOrEmpty(record.UserName)) {
                        var auth = await smtp.AuthenticateAsync(smtp.Credential!, false, ct).ConfigureAwait(false);
                        if (!auth.Status) throw new InvalidOperationException(auth.Error ?? "SMTP authentication failed.");
                    }
                }
                await smtp.Client.SendAsync(message, ct).ConfigureAwait(false);
                // Sent-log failures must not turn an accepted provider send into a retry.
                var result = new SmtpResult(true, EmailAction.Send, SentMessageRecipients.Serialize(message.To),
                    Helpers.GetEmailAddress(message.From.Mailboxes.FirstOrDefault() ?? (object)string.Empty),
                    server ?? string.Empty, record.Port ?? smtp.Port, TimeSpan.Zero) {
                    DeliveryAccepted = true, MessageId = record.MessageId
                };
                await smtp.RecordPostSendErrorAsync(result, "Sent-log persistence", async () => {
                    if (smtp.SentMessageRepository != null) {
                        await smtp.SentMessageRepository.SaveAsync(new SentMessageRecord {
                            MessageId = message.MessageId ?? record.MessageId,
                            Recipients = SentMessageRecipients.Serialize(message.To),
                            Subject = message.Subject ?? string.Empty, Timestamp = DateTimeOffset.UtcNow
                        }, CancellationToken.None).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            } finally {
                try { smtp.Disconnect(); } catch (Exception exception) { smtp.LogWarning($"ProcessPendingMessages - Disconnect failed: {exception.Message}"); }
                smtp.SkipCertificateValidation = originalSkip;
                smtp.ServerCertificateValidationCallback = originalCallback;
                smtp.CheckCertificateRevocation = originalRevocation;
                smtp.Timeout = originalTimeout;
                smtp._activeSecureSocketOptions = originalSecureOptions;
                smtp._activeUseSsl = originalUseSsl;
                smtp.ConnectionPoolIdentity = originalIdentity;
                smtp.Credential = originalCredential;
            }
        }
    }
}

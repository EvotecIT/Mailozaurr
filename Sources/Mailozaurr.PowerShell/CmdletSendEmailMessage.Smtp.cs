using Mailozaurr;
using System;
using System.Management.Automation;
using System.Linq;
using System.Net;
using System.Threading;

namespace Mailozaurr.PowerShell;

public sealed partial class CmdletSendEmailMessage {
    private void ProcessSmtp(string fromEmail, string fromName) {
        // Create a LogCollector to capture logs from async operations
        var logCollector = new LogCollector();

        bool useDeliverySession = (Server?.Length ?? 0) > 1 || DeliverySession != null
            || (Server?.Any(server => server != null && server.Contains("://")) ?? false)
            || MyInvocation.BoundParameters.ContainsKey(nameof(Strategy))
            || MyInvocation.BoundParameters.ContainsKey(nameof(CircuitBreakMinutes));
        Smtp smtpClient = useDeliverySession ? new Smtp() : CreateLoggedSmtpTransport();

        try {
            // Attach the LogCollector to the SMTP client's logger
            smtpClient.LogCollector = logCollector;

            if (!string.IsNullOrWhiteSpace(SentLogPath)) {
                smtpClient.SentMessageRepository = new FileSentMessageRepository(SentLogPath!);
            }
            smtpClient.From = Helpers.GetFromObject(fromEmail, fromName);
            smtpClient.ReplyTo = ReplyTo;
            smtpClient.Cc = Cc;
            smtpClient.Bcc = Bcc;
            smtpClient.To = To;
            smtpClient.Subject = Subject ?? string.Empty;
            smtpClient.Priority = Priority;

            smtpClient.DeliveryNotificationOption = DeliveryNotificationOption;
            smtpClient.DeliveryStatusNotificationType = DeliveryStatusNotificationType;

            smtpClient.CheckCertificateRevocation = !SkipCertificateRevocation;
            smtpClient.SkipCertificateValidation = SkipCertificateValidation;
            if (HTML != null) smtpClient.HtmlBody = string.Join("", HTML);
            if (Text != null) smtpClient.TextBody = string.Join("", Text);

            smtpClient.Attachments = AttachmentInputConverter.Convert(Attachment);
            smtpClient.InlineAttachments = AttachmentInputConverter.Convert(InlineAttachment);
            if (Headers != null) smtpClient.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
            smtpClient.Timeout = Timeout;

            smtpClient.ErrorAction = errorAction;
            smtpClient.RetryCount = RetryCount;
            smtpClient.RetryDelayMilliseconds = RetryDelayMilliseconds;
            smtpClient.RetryDelayBackoff = RetryDelayBackoff;
            smtpClient.RetryAlways = RetryAlways.IsPresent;
            smtpClient.MaxDelayMilliseconds = MaxDelayMilliseconds;
            smtpClient.JitterMilliseconds = JitterMilliseconds;
            smtpClient.UseConnectionPool = UseConnectionPool.IsPresent;
            if (UseDefaultCredentials) {
                smtpClient.ConnectionPoolIdentity = "default";
            } else if (Credential != null) {
                smtpClient.ConnectionPoolIdentity = Credential.UserName;
            } else if (!string.IsNullOrWhiteSpace(Username)) {
                smtpClient.ConnectionPoolIdentity = Username;
            }
            if (useDeliverySession) {
                ProcessSmtpDeliverySession(smtpClient, logCollector);
                return;
            }
            smtpClient.DryRun = !ShouldProcess(smtpClient.SentTo, "Sending email message");
            if (smtpClient.DryRun) {
                logCollector.LogVerbose("Send-EmailMessage - Skipping authentication");
                var useSslFlagDryRun = UseSsl.IsPresent && !this.MyInvocation.BoundParameters.ContainsKey(nameof(SecureSocketOptions));
                smtpClient.Connect(Server?.FirstOrDefault() ?? string.Empty, Port, SecureSocketOptions, useSslFlagDryRun);
                smtpClient.CreateMessage(CancellationToken.None);
                var dryRunStatus = smtpClient.Send();
                LogEmitter.EmitLogs(logCollector, this);
                if (!Suppress) {
                    WriteObject(dryRunStatus);
                }
                return;
            }

            var useSslFlag = UseSsl.IsPresent && !this.MyInvocation.BoundParameters.ContainsKey(nameof(SecureSocketOptions));
            if (!UseDefaultCredentials && Credential != null) {
                smtpClient.ConfigureAuthentication(new NetworkCredential(Credential.UserName, Credential.Password),
                    OAuth2 ? ProtocolAuthMode.OAuth2 : ProtocolAuthMode.Basic);
            } else if (!UseDefaultCredentials && !string.IsNullOrWhiteSpace(Username)) {
                smtpClient.ConfigureAuthentication(new NetworkCredential(Username,
                    smtpClient.ConvertSecureStringToPlainString(Password ?? string.Empty, AsSecureString)));
            }
            var status = smtpClient.Connect(Server?.FirstOrDefault() ?? string.Empty, Port, SecureSocketOptions, useSslFlag);

            // Emit logs after Connect operation
            LogEmitter.EmitLogs(logCollector, this);

            if (!status.Status) {
                if (!Suppress) {
                    WriteObject(status);
                }

                return;
            }

            smtpClient.CreateMessage(CancellationToken.None);

            status = ApplySmtpEncryption(smtpClient, status);
            if (!status.Status) {
                if (!Suppress) WriteObject(status);
                return;
            }

            if (UseDefaultCredentials) {
                status = smtpClient.AuthenticateDefaultCredentials();
            } else if (Credential != null) {
                NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
                status = smtpClient.Authenticate(networkCredential, OAuth2);
            } else if (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password)) {
                status = smtpClient.Authenticate(Username ?? string.Empty, Password ?? string.Empty, AsSecureString, AuthenticationMechanism);
            } else {
                logCollector.LogVerbose("Send-EmailMessage - Skipping authentication");
                status = new SmtpResult(true, EmailAction.Authenticate, smtpClient.SentTo, smtpClient.SentFrom, smtpClient.Server, smtpClient.Port, smtpClient.Stopwatch.Elapsed, "Authentication skipped");
            }

            // Emit logs after Authentication operation
            LogEmitter.EmitLogs(logCollector, this);

            if (!status.Status) {
                if (!Suppress) {
                    WriteObject(status);
                }

                return;
            }

            status = smtpClient.Send();

            // Emit logs after Send operation
            LogEmitter.EmitLogs(logCollector, this);

            if (!Suppress) {
                WriteObject(status);
            }

            var mimePath = MimeMessagePath;
            if (mimePath != null && mimePath.Length > 0) {
                smtpClient.SaveMessage(mimePath);
            }

        } finally {
            smtpClient.Dispose();
        }
    }

    private Smtp CreateLoggedSmtpTransport() => new Smtp(LogPath ?? string.Empty, LogConsole, LogObject,
        LogTimestamps, LogSecrets, LogTimeStampsFormat, LogServerPrefix, LogClientPrefix, LogOverwrite);

    private SmtpResult ApplySmtpEncryption(Smtp smtpClient, SmtpResult status) {
        if (SignOrEncrypt != EmailActionEncryption.None) {
            if (SignOrEncrypt == EmailActionEncryption.PGPEncrypt && PublicKeyPath != null) {
                status = smtpClient.PgpEncrypt(PublicKeyPath);
            } else if (SignOrEncrypt == EmailActionEncryption.PGPSign && PublicKeyPath != null && PrivateKeyPath != null) {
                status = smtpClient.PgpSign(PublicKeyPath, PrivateKeyPath, PrivateKeyPassword ?? string.Empty, PrivateKeyPasswordAsSecureString);
            } else if (SignOrEncrypt == EmailActionEncryption.PGPSignAndEncrypt && PublicKeyPath != null && PrivateKeyPath != null) {
                status = smtpClient.PgpSignAndEncrypt(PublicKeyPath, PrivateKeyPath, PrivateKeyPassword ?? string.Empty, PrivateKeyPasswordAsSecureString);
            } else if (Certificate != null) {
                status = smtpClient.Encrypt(SignOrEncrypt, Certificate);
            } else if (CertificateThumbprint != null) {
                status = smtpClient.Encrypt(SignOrEncrypt, CertificateThumbprint);
            } else if (CertificatePath != null && CertificatePassword != null) {
                status = smtpClient.Encrypt(SignOrEncrypt, CertificatePath, CertificatePassword,
                    CertificatePasswordAsSecureString);
            }

        }
        return status;
    }
}

using MailKit.Security;
using Mailozaurr;
using System;
using System.Management.Automation;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

public sealed partial class CmdletSendEmailMessage {
    /// <summary>Composes once and delegates all endpoint selection and retries to the core session.</summary>
    private void ProcessSmtpDeliverySession(Smtp smtpClient, LogCollector logCollector) {
        bool useSsl = UseSsl.IsPresent && !MyInvocation.BoundParameters.ContainsKey(nameof(SecureSocketOptions));
        var secureOptions = useSsl && SecureSocketOptions == SecureSocketOptions.Auto
            ? SecureSocketOptions.StartTls : SecureSocketOptions;
        var options = new SmtpDeliveryOptions {
            Servers = (Server ?? Array.Empty<string>()).Select(server => SmtpEndpoint.Parse(server, Port, secureOptions)).ToList(),
            Strategy = Strategy,
            CircuitBreakMinutes = CircuitBreakMinutes,
            MaxAttempts = (int)Math.Min(int.MaxValue, Math.Max((long)(Server?.Length ?? 0), (long)RetryCount + 1))
        };
        smtpClient.CreateMessage(CancellationToken.None);
        if (!ShouldProcess(smtpClient.SentTo, "Sending email message")) {
            if (!Suppress) {
                WriteObject(new SmtpResult(false, EmailAction.Send, smtpClient.SentTo, smtpClient.SentFrom,
                    Server?.FirstOrDefault() ?? string.Empty, Port, smtpClient.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
            }
            return;
        }
        var status = ApplySmtpEncryption(smtpClient,
            new SmtpResult(true, EmailAction.Send, smtpClient.SentTo, smtpClient.SentFrom,
                Server?.FirstOrDefault() ?? string.Empty, Port, smtpClient.Stopwatch.Elapsed, ""));
        if (!status.Status) {
            if (!Suppress) WriteObject(status);
            return;
        }
        var networkCredential = Credential != null ? new NetworkCredential(Credential.UserName, Credential.Password) : null;
        string password = networkCredential?.Password ?? smtpClient.ConvertSecureStringToPlainString(Password ?? string.Empty, AsSecureString);
        var request = new SmtpSessionRequest {
            TimeoutMs = Timeout,
            RetryCount = 0,
            RetryDelayMilliseconds = RetryDelayMilliseconds,
            RetryDelayBackoff = RetryDelayBackoff,
            MaxDelayMilliseconds = MaxDelayMilliseconds,
            JitterMilliseconds = JitterMilliseconds,
            RetryAlways = RetryAlways.IsPresent,
            SkipCertificateValidation = SkipCertificateValidation,
            SkipCertificateRevocation = SkipCertificateRevocation,
            Authenticate = UseDefaultCredentials || networkCredential != null || !string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password),
            UserName = networkCredential?.UserName ?? Username ?? string.Empty,
            Password = password,
            AuthenticationContext = UseDefaultCredentials
                ? "default:" + Environment.UserDomainName + "\\" + Environment.UserName
                : networkCredential != null ? "credential" : "mechanism:" + AuthenticationMechanism,
            AuthMode = OAuth2 ? ProtocolAuthMode.OAuth2 : ProtocolAuthMode.Basic,
            AuthenticateWithCancellationAsync = (client, token) => {
                token.ThrowIfCancellationRequested();
                if (UseDefaultCredentials) return Task.FromResult(client.AuthenticateDefaultCredentials());
                if (networkCredential != null) return client.AuthenticateAsync(networkCredential, OAuth2, token);
                if (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password)) {
                    return Task.FromResult(client.Authenticate(Username ?? string.Empty, password, false, AuthenticationMechanism));
                }
                logCollector.LogVerbose("Send-EmailMessage - Skipping authentication");
                return Task.FromResult(new SmtpResult(true, EmailAction.Authenticate, smtpClient.SentTo, smtpClient.SentFrom,
                    client.Server, client.Port, smtpClient.Stopwatch.Elapsed, "Authentication skipped"));
            }
        };
        SmtpDeliverySession? localSession = null;
        try {
            var session = DeliverySession ?? (localSession = new SmtpDeliverySession());
            bool protocolLogging = !string.IsNullOrWhiteSpace(LogPath) || LogConsole || LogObject;
            if (protocolLogging) {
                logCollector.LogVerbose("Send-EmailMessage - Protocol logging uses invocation-owned connections; rotation and circuit state are retained, but these connections close after this send.");
            }
            status = session.SendAsync(smtpClient.Message, request, options, client => {
                client.LogCollector = logCollector;
                client.SentMessageRepository = smtpClient.SentMessageRepository;
                // Wrapper setters ignore null; reset omitted options on reused connections.
                client.Client.DeliveryNotificationOption = DeliveryNotificationOption;
                client.Client.DeliveryStatusNotificationType = DeliveryStatusNotificationType
                    ?? MailKit.Net.Smtp.DeliveryStatusNotificationType.Unspecified;
            }, protocolLogging ? CreateLoggedSmtpTransport : null, CancellationToken.None).GetAwaiter().GetResult();
            if (status.DeliveryAccepted || status.DeliveryUnknown) smtpClient.MarkTransportAttempted();
            if (!string.IsNullOrEmpty(MimeMessagePath)) {
                try {
                    smtpClient.SaveMessage(MimeMessagePath!);
                } catch (Exception exception) when (status.DeliveryAccepted) {
                    string error = "MIME save: " + exception.Message;
                    status.PostSendErrors.Add(error);
                    logCollector.LogWarning("Send-EmailMessage - " + error);
                }
            }
            if (!Suppress) WriteObject(status);
        } finally {
            localSession?.Dispose();
            LogEmitter.EmitLogs(logCollector, this);
        }
    }
}

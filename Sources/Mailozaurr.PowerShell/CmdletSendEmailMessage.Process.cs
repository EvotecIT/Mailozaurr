using System;
using System.Management.Automation;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Threading;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

public sealed partial class CmdletSendEmailMessage : PSCmdlet
{
    private ActionPreference errorAction;
    private InternalLogger? _logger;
    private LogCollector? _logCollector;
    private EventHandler<LogEventArgs>? _onVerbose;
    private EventHandler<LogEventArgs>? _onWarning;
    private EventHandler<LogEventArgs>? _onError;
    private EventHandler<LogEventArgs>? _onInformation;

    /// <summary>
    /// Begin block
    /// </summary>
    protected override void BeginProcessing() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        _logger = new InternalLogger();
        _logCollector = new LogCollector();

        _onVerbose = (_, e) => _logCollector!.LogVerbose(e.FullMessage);
        _onWarning = (_, e) => _logCollector!.LogWarning(e.FullMessage);
        _onError =  (_, e) => _logCollector!.LogError(e.FullMessage);
        _onInformation = (_, e) => _logCollector!.LogInformation(e.FullMessage);

        _logger.OnVerboseMessage += _onVerbose;
        _logger.OnWarningMessage += _onWarning;
        _logger.OnErrorMessage += _onError;
        _logger.OnInformationMessage += _onInformation;

        LoggingMessages.Logger = _logger;

        // Get the error action preference as user requested
        // It first sets the error action to the default error action preference
        // If the user has specified the error action, it will set the error action to the user specified error action
        errorAction = (ActionPreference)this.SessionState.PSVariable.GetValue("ErrorActionPreference");
            if (this.MyInvocation.BoundParameters.ContainsKey("ErrorAction")) {
            string? errorActionString = this.MyInvocation.BoundParameters["ErrorAction"]?.ToString();
            if (errorActionString != null && Enum.TryParse(errorActionString, true, out ActionPreference actionPreference)) {
                errorAction = actionPreference;
            }
        }

        SmtpConnectionPool.MaxPoolSize = ConnectionPoolSize;
        SmtpConnectionPool.PoolingEnabled = UseConnectionPool.IsPresent;
    }
    /// <summary>
    /// Process the record.
    /// </summary>
    protected override void ProcessRecord() {
        Attachment = FilterExistingPaths(Attachment, nameof(Attachment));
        InlineAttachment = FilterExistingPaths(InlineAttachment, nameof(InlineAttachment));
        var (fromEmailRaw, fromNameRaw) = Helpers.GetEmailAndName(From);
        string fromEmail = fromEmailRaw ?? string.Empty;
        string fromName = fromNameRaw ?? string.Empty;

        try {
            if (SendGrid || EmailProvider == EmailProvider.SendGrid) {
                ProcessSendGrid(fromEmail, fromName);
            } else if (EmailProvider == EmailProvider.Mailgun) {
                ProcessMailgun(fromEmail, fromName);
            } else if (EmailProvider == EmailProvider.SES) {
                ProcessSes(fromEmail, fromName);
            } else if (EmailProvider == EmailProvider.Gmail) {
                ProcessGmail(fromEmail, fromName);
            } else if (Graph) {
                ProcessGraph(fromEmail, fromName);
            } else if (MgGraphRequest) {
                ProcessMgGraphRequest(fromEmail, fromName).GetAwaiter().GetResult();
            } else {
                ProcessSmtp(fromEmail, fromName);
            }
        } finally {
            if (_logCollector != null) {
                LogEmitter.EmitLogs(_logCollector, this);
            }
        }
    }

    private void ProcessSendGrid(string fromEmail, string fromName) {
        if (Credential == null) {
            throw new InvalidOperationException("Credential is required for SendGrid processing.");
        }
        var logCollector = new LogCollector();
        SendGridClient sendGrid = new SendGridClient();
        sendGrid.LogCollector = logCollector;
        sendGrid.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) sendGrid.Bcc = Bcc.ToList();
        if (Cc != null) sendGrid.Cc = Cc.ToList();
        if (To != null) sendGrid.To = To.ToList();
        sendGrid.ReplyTo = ReplyTo;
        sendGrid.Subject = Subject ?? string.Empty;
        if (Text != null) sendGrid.Text = string.Join("", Text);
        if (HTML != null) sendGrid.Html = string.Join("", HTML);
        sendGrid.Priority = Priority;
        if (Attachment != null) {
            sendGrid.Attachment = Attachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (Headers != null) sendGrid.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        sendGrid.SeparateTo = SeparateTo;
        sendGrid.ErrorAction = errorAction;
        sendGrid.RetryCount = RetryCount;
        sendGrid.RetryDelayMilliseconds = RetryDelayMilliseconds;
        sendGrid.RetryDelayBackoff = RetryDelayBackoff;
        sendGrid.RetryAlways = RetryAlways.IsPresent;
        NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
        sendGrid.Credentials = networkCredential;
        sendGrid.CreateMessage();
        if (ShouldProcess(sendGrid.SentTo, "Sending email message via SendGrid")) {
            var result = sendGrid.SendEmailAsync().GetAwaiter().GetResult();
            LogEmitter.EmitLogs(logCollector, this);
            if (!Suppress) {
                WriteObject(result);
            }
        } else if (!Suppress) {
            WriteObject(new SmtpResult(false, EmailAction.Send, sendGrid.SentTo, sendGrid.SentFrom, "SendGridApi", 0, sendGrid.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
        }
    }

    private void ProcessMailgun(string fromEmail, string fromName) {
        if (Credential == null) {
            throw new InvalidOperationException("Credential is required for Mailgun processing.");
        }
        var logCollector = new LogCollector();
        using MailgunClient mailgun = new MailgunClient();
        mailgun.LogCollector = logCollector;
        mailgun.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) mailgun.Bcc = Bcc.ToList();
        if (Cc != null) mailgun.Cc = Cc.ToList();
        if (To != null) mailgun.To = To.ToList();
        mailgun.ReplyTo = ReplyTo;
        mailgun.Subject = Subject ?? string.Empty;
        if (Text != null) mailgun.Text = string.Join("", Text);
        if (HTML != null) mailgun.Html = string.Join("", HTML);
        if (Attachment != null) {
            mailgun.Attachment = Attachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (InlineAttachment != null) {
            mailgun.InlineAttachment = InlineAttachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (Headers != null) mailgun.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        mailgun.ErrorAction = errorAction;
        mailgun.RetryCount = RetryCount;
        mailgun.RetryDelayMilliseconds = RetryDelayMilliseconds;
        mailgun.RetryDelayBackoff = RetryDelayBackoff;
        mailgun.RetryAlways = RetryAlways.IsPresent;
        NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
        mailgun.Credentials = networkCredential;
        if (ShouldProcess(mailgun.SentTo, "Sending email message via Mailgun")) {
            var result = mailgun.SendEmailAsync().GetAwaiter().GetResult();
            LogEmitter.EmitLogs(logCollector, this);
            if (!Suppress) {
                WriteObject(result);
            }
        } else if (!Suppress) {
            WriteObject(new SmtpResult(false, EmailAction.Send, mailgun.SentTo, mailgun.SentFrom, "MailgunApi", 0, mailgun.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
        }
    }

    private void ProcessSes(string fromEmail, string fromName) {
        if (Credential == null) {
            throw new InvalidOperationException("Credential is required for SES processing.");
        }
        var logCollector = new LogCollector();
        using SesClient ses = new SesClient();
        ses.LogCollector = logCollector;
        ses.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) ses.Bcc = Bcc.ToList();
        if (Cc != null) ses.Cc = Cc.ToList();
        if (To != null) ses.To = To.ToList();
        ses.ReplyTo = ReplyTo;
        ses.Subject = Subject ?? string.Empty;
        if (Text != null) ses.Text = string.Join("", Text);
        if (HTML != null) ses.Html = string.Join("", HTML);
        if (Attachment != null) {
            ses.Attachment = Attachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (InlineAttachment != null) {
            ses.InlineAttachment = InlineAttachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (Headers != null) ses.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        ses.ErrorAction = errorAction;
        ses.RetryCount = RetryCount;
        ses.RetryDelayMilliseconds = RetryDelayMilliseconds;
        ses.RetryDelayBackoff = RetryDelayBackoff;
        ses.RetryAlways = RetryAlways.IsPresent;
        var region = Region;
        if (region != null && region.Length > 0) {
            ses.Region = region;
        }
        NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
        ses.Credentials = networkCredential;
        if (ShouldProcess(ses.SentTo, "Sending email message via SES")) {
            var result = ses.SendEmailAsync().GetAwaiter().GetResult();
            LogEmitter.EmitLogs(logCollector, this);
            if (!Suppress) {
                WriteObject(result);
            }
        } else if (!Suppress) {
            WriteObject(new SmtpResult(false, EmailAction.Send, ses.SentTo, ses.SentFrom, "SESApi", 0, ses.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
        }
    }

    private void ProcessGmail(string fromEmail, string fromName) {
        var smtp = new Smtp();
        smtp.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) smtp.Bcc = Bcc.ToList();
        if (Cc != null) smtp.Cc = Cc.ToList();
        if (To != null) smtp.To = To.ToList();
        smtp.ReplyTo = ReplyTo;
        smtp.Subject = Subject ?? string.Empty;
        if (Text != null) smtp.TextBody = string.Join("", Text);
        if (HTML != null) smtp.HtmlBody = string.Join("", HTML);
        smtp.Attachments = Attachment?.ToList();
        smtp.InlineAttachments = InlineAttachment?.ToList();
        smtp.Priority = Priority;
        if (Headers != null) smtp.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        smtp.CreateMessage(CancellationToken.None);

        if (Credential == null) {
            throw new InvalidOperationException("Credential is required for Gmail processing.");
        }

        var net = Credential.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };

        var client = new GmailApiClient(oauth);
        try {
            if (ShouldProcess(smtp.SentTo, "Sending email message via Gmail API")) {
                var account = GmailAccount;
                if (account == null || account.Length == 0) {
                    throw new ArgumentException("GmailAccount is required when using Gmail API.", nameof(GmailAccount));
                }
                var msg = client.SendAsync(account, smtp.Message).GetAwaiter().GetResult();
                if (!Suppress) {
                    WriteObject(new SmtpResult(true, EmailAction.Send, smtp.SentTo, smtp.SentFrom, "GmailApi", 0, smtp.Stopwatch.Elapsed, msg.Id));
                }
            } else if (!Suppress) {
                WriteObject(new SmtpResult(false, EmailAction.Send, smtp.SentTo, smtp.SentFrom, "GmailApi", 0, smtp.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
            }
        } finally {
            smtp.Dispose();
        }
    }

    private void ProcessGraph(string fromEmail, string fromName) {
        if (Credential == null) {
            throw new InvalidOperationException("Credential is required for Graph processing.");
        }
        using Graph graph = new Graph();
        graph.ChunkSize = ChunkSize;
        graph.From = Helpers.GetFromObject(fromEmail, fromName);
        graph.To = To;
        graph.Cc = Cc;
        graph.Bcc = Bcc;
        graph.ReplyTo = ReplyTo;
        graph.Subject = Subject ?? string.Empty;
        graph.DoNotSaveToSentItems = DoNotSaveToSentItems;
        graph.ErrorAction = errorAction;
        graph.RetryCount = RetryCount;
        graph.RetryDelayMilliseconds = RetryDelayMilliseconds;
        graph.RetryDelayBackoff = RetryDelayBackoff;
        graph.RetryAlways = RetryAlways.IsPresent;
        graph.RequestReadReceipt = RequestReadReceipt;
        graph.RequestDeliveryReceipt = RequestDeliveryReceipt;
        graph.HTML = string.Join("", HTML ?? Array.Empty<string>());
        graph.ContentType = "HTML";
        graph.Attachments = Attachment;
        if (Headers != null) graph.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        graph.CreateAttachments();
        long graphSize = GetTotalAttachmentSize(graph.ConvertedAttachments);
        if (graphSize > GraphAttachmentLimitBytes) {
            WriteError(new ErrorRecord(
                new ArgumentException("Attachments exceed Graph limit of 150MB."),
                "GraphAttachmentLimitExceeded",
                ErrorCategory.InvalidData,
                null));
            LogEmitter.EmitLogs(graph.LogCollector, this);
            return;
        }

        if (!ShouldProcess(graph.SentTo, "Sending email message via Graph")) {
            LoggingMessages.Logger.WriteVerbose("Send-EmailMessage - Skipping authentication");
            if (!Suppress) {
                WriteObject(new SmtpResult(false, EmailAction.Send, graph.SentTo, graph.SentFrom, "GraphAPI", 0, graph.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
            }
            LogEmitter.EmitLogs(graph.LogCollector, this);
            return;
        }

        NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
        graph.Authenticate(networkCredential);
        try {
            var status = graph.ConnectO365GraphAsync().GetAwaiter().GetResult();
            if (!status.Status) {
                if (!Suppress) {
                    WriteObject(status);
                }
                LogEmitter.EmitLogs(graph.LogCollector, this);
                return;
            }
            status = graph.IsLargerAttachment
                ? graph.SendMessageDraftAsync().GetAwaiter().GetResult()
                : graph.SendMessageAsync().GetAwaiter().GetResult();
            if (!Suppress) {
                WriteObject(status);
            }
        } catch (GraphApiException ex) {
            WriteError(new ErrorRecord(ex, "GraphApiError", ErrorCategory.InvalidOperation, null));
        }

        LogEmitter.EmitLogs(graph.LogCollector, this);
    }

    private async Task ProcessMgGraphRequest(string fromEmail, string fromName) {
        using Graph graph = new Graph();
        graph.ChunkSize = ChunkSize;
        graph.From = Helpers.GetFromObject(fromEmail, fromName);
        graph.To = To;
        graph.Cc = Cc;
        graph.Bcc = Bcc;
        graph.ReplyTo = ReplyTo;
        graph.Subject = Subject ?? string.Empty;
        graph.DoNotSaveToSentItems = DoNotSaveToSentItems;
        graph.ErrorAction = errorAction;
        graph.RetryCount = RetryCount;
        graph.RetryDelayMilliseconds = RetryDelayMilliseconds;
        graph.RetryDelayBackoff = RetryDelayBackoff;
        graph.RequestReadReceipt = RequestReadReceipt;
        graph.RequestDeliveryReceipt = RequestDeliveryReceipt;
        graph.HTML = string.Join("", HTML ?? Array.Empty<string>());
        graph.ContentType = "HTML";
        graph.Attachments = Attachment;
        if (Headers != null) graph.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        graph.CreateAttachments();
        long size = GetTotalAttachmentSize(graph.ConvertedAttachments);
        if (size > GraphAttachmentLimitBytes) {
            WriteError(new ErrorRecord(
                new ArgumentException("Attachments exceed Graph limit of 150MB."),
                "GraphAttachmentLimitExceeded",
                ErrorCategory.InvalidData,
                null));
            LogEmitter.EmitLogs(graph.LogCollector, this);
            return;
        }
        if (graph.IsLargerAttachment) {
            var json = graph.CreateDraftForMg();
            var draftMessageId = InvokeMgGraphRequestPOST1($"v1.0/users/{graph.From}/mailfolders/drafts/messages", EmailAction.SendDraftMessage, json, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
            await graph.PrepareAttachments();
            foreach (var attachment in graph.AttachmentsPlaceHolders) {
                var uploadUrl = InvokeMgGraphRequestPOST(attachment.Json, EmailAction.Send, attachment.Json, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
                if (uploadUrl != string.Empty) {
                    await InvokeMgGraphRequestPUT(uploadUrl, EmailAction.SendAttachment, attachment, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
                } else {
                    graph.LogCollector.LogVerbose("PlaceHolders not working?");
                }
            }
            var sendUri = MicrosoftGraphUtils.BuildGraphUri(
                GraphEndpoint.V1,
                $"/users('{graph.SentFrom}')/messages/{draftMessageId}/send");
            InvokeMgGraphRequest(sendUri, EmailAction.Send, graph.MessageJson, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
            LogEmitter.EmitLogs(graph.LogCollector, this);
        } else {
            graph.CreateMessage();
            InvokeMgGraphRequest($"v1.0/users/{fromEmail}/sendMail", EmailAction.Send, graph.MessageJson, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
            LogEmitter.EmitLogs(graph.LogCollector, this);
        }
    }

    private void ProcessSmtp(string fromEmail, string fromName) {
        Smtp smtpClient = new Smtp(LogPath ?? string.Empty, LogConsole, LogObject, LogTimestamps, LogSecrets, LogTimeStampsFormat, LogServerPrefix, LogClientPrefix, LogOverwrite);
        string sentLogPath;
        var providedSentLogPath = SentLogPath;
        if (providedSentLogPath == null || providedSentLogPath.Trim().Length == 0) {
            sentLogPath = Path.Combine(Path.GetTempPath(), "Mailozaurr", "sentlog.json");
        } else {
            sentLogPath = providedSentLogPath;
        }
        smtpClient.SentMessageRepository = new FileSentMessageRepository(sentLogPath);
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

        smtpClient.Attachments = Attachment?.ToList();
        smtpClient.InlineAttachments = InlineAttachment?.ToList();
        if (Headers != null) smtpClient.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        smtpClient.Timeout = Timeout;

        smtpClient.ErrorAction = errorAction;
        smtpClient.RetryCount = RetryCount;
        smtpClient.RetryDelayMilliseconds = RetryDelayMilliseconds;
        smtpClient.RetryDelayBackoff = RetryDelayBackoff;
        smtpClient.RetryAlways = RetryAlways.IsPresent;

        if (!ShouldProcess(smtpClient.SentTo, "Sending email message")) {
            LoggingMessages.Logger.WriteVerbose("Send-EmailMessage - Skipping authentication");
            if (!Suppress) {
                WriteObject(new SmtpResult(false, EmailAction.Send, smtpClient.SentTo, smtpClient.SentFrom, Server ?? string.Empty, Port, TimeSpan.Zero, string.Empty, "Email not sent (WhatIf)"));
            }
            return;
        }

        var useSslFlag = UseSsl.IsPresent && !this.MyInvocation.BoundParameters.ContainsKey(nameof(SecureSocketOptions));
        var status = smtpClient.Connect(Server ?? string.Empty, Port, SecureSocketOptions, useSslFlag);
        if (!status.Status) {
            if (!Suppress) {
                WriteObject(status);
            }

            smtpClient.Dispose();
            return;
        }

        smtpClient.CreateMessage(CancellationToken.None);

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

            if (!status.Status) {
                if (!Suppress) {
                    WriteObject(status);
                }

                smtpClient.Dispose();
                return;
            }
        }

        if (UseDefaultCredentials) {
            status = smtpClient.AuthenticateDefaultCredentials();
        } else if (Credential != null) {
            NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
            status = smtpClient.Authenticate(networkCredential, OAuth2);
        } else if (!string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password)) {
            status = smtpClient.Authenticate(Username ?? string.Empty, Password ?? string.Empty, AsSecureString, AuthenticationMechanism);
        } else {
            LoggingMessages.Logger.WriteVerbose("Send-EmailMessage - Skipping authentication");
            status = new SmtpResult(true, EmailAction.Authenticate, smtpClient.SentTo, smtpClient.SentFrom, smtpClient.Server, smtpClient.Port, smtpClient.Stopwatch.Elapsed, "Authentication skipped");
        }

        if (!status.Status) {
            if (!Suppress) {
                WriteObject(status);
            }

            smtpClient.Dispose();
            return;
        }

        status = smtpClient.Send();
        if (!Suppress) {
            WriteObject(status);
        }

        var mimePath = MimeMessagePath;
        if (mimePath != null && mimePath.Length > 0) {
            smtpClient.SaveMessage(mimePath);
        }

        smtpClient.Dispose();
    }


    /// <summary>
    /// Method to invoke the MgGraphRequest cmdlet
    /// </summary>
    /// <param name="uri"></param>
    /// <param name="action"></param>
    /// <param name="jsonBody"></param>
    /// <param name="sentFrom"></param>
    /// <param name="sentTo"></param>
    /// <param name="elapsed"></param>
    private void InvokeMgGraphRequest(string uri, EmailAction action, string jsonBody, string sentFrom, string sentTo, TimeSpan elapsed) {
        var parameters = new Hashtable {
            { "Method", "POST" },
            { "Uri", uri },
            { "ContentType", "application/json; charset=UTF-8"},
            { "Body", jsonBody }
        };

        var powerShell = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        powerShell.AddCommand("Invoke-MgGraphRequest");
        powerShell.AddParameters(parameters);
        try {
            var result = powerShell.Invoke();
            if (!Suppress) {
                WriteObject(new SmtpResult(true, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ""));
            }
        } catch (RuntimeException ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
            if (errorAction == ActionPreference.Stop) {
                throw;
            }
            if (!Suppress) {
                var result = new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message) {
                    GraphError = GraphApiErrorParser.Parse(ex.Message)
                };
                WriteObject(result);
            }
        }
    }

    private async Task InvokeMgGraphRequestPUT(string uri, EmailAction action, GraphAttachmentPlaceHolder attachment, string sentFrom, string sentTo, TimeSpan elapsed) {
        foreach (var body in attachment.Content) {
            var parameters = new Hashtable {
                { "Method", "PUT" },
                { "Uri", uri },
                { "ContentType", "application/json; charset=UTF-8" },
                { "Body",  await body.ReadAsByteArrayAsync() },
                { "Headers", new Hashtable {
                         { "Content-Range", body.Headers.ContentRange },
                        // { "AnchorMailbox", sentFrom }
                    }
                }
            };

            var powerShell = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            powerShell.AddCommand("Invoke-MgGraphRequest");
            powerShell.AddParameters(parameters);
            try {
                var results = powerShell.Invoke();
            } catch (RuntimeException ex) {
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
                if (errorAction == ActionPreference.Stop) {
                    throw;
                }

                if (!Suppress) {
                    var result = new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message) {
                        GraphError = GraphApiErrorParser.Parse(ex.Message)
                    };
                    WriteObject(result);
                }
            }
        }
    }


    private string InvokeMgGraphRequestPOST(string uri, EmailAction action, string jsonBody, string sentFrom, string sentTo, TimeSpan elapsed) {
        var parameters = new Hashtable {
            { "Method", "POST" },
            { "Uri", uri },
            { "ContentType", "application/json; charset=UTF-8"},
            { "Body", jsonBody }
        };

        var powerShell = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        powerShell.AddCommand("Invoke-MgGraphRequest");
        powerShell.AddParameters(parameters);
        try {
            var results = powerShell.Invoke();
            if (results.Count > 0) {
                // Assuming the first result contains the property you're interested in
                var result = results[0];
                //var result = results[0];
                if (result.BaseObject is IDictionary dictionary && dictionary.Contains("uploadUrl")) {
                    return dictionary["uploadUrl"]?.ToString() ?? string.Empty;
                } else {
                    // Handle the case where the property is not present
                    throw new InvalidOperationException("The result does not contain an 'uploadUrl' property.");
                }
            } else {
                // Handle the case where no results were returned
                throw new InvalidOperationException("No results were returned from the Invoke-MgGraphRequest command.");
            }
        } catch (RuntimeException ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
            if (errorAction == ActionPreference.Stop) {
                throw;
            }
            if (!Suppress) {
                var result = new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message) {
                    GraphError = GraphApiErrorParser.Parse(ex.Message)
                };
                WriteObject(result);
            }
        }

        return "";
    }

    private string InvokeMgGraphRequestPOST1(string uri, EmailAction action, string jsonBody, string sentFrom, string sentTo, TimeSpan elapsed) {
        var parameters = new Hashtable {
            { "Method", "POST" },
            { "Uri", uri },
            { "ContentType", "application/json; charset=UTF-8"},
            { "Body", jsonBody }
        };
        var powerShell = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        powerShell.AddCommand("Invoke-MgGraphRequest");
        powerShell.AddParameters(parameters);
        try {
            var results = powerShell.Invoke();
            if (results.Count > 0) {
                // Assuming the first result contains the property you're interested in
                var result = results[0];
                if (result.BaseObject is IDictionary dictionary && dictionary.Contains("id")) {
                    return dictionary["id"]?.ToString() ?? string.Empty;
                } else {
                    // Handle the case where the property is not present
                    throw new InvalidOperationException("The result does not contain an 'id' property.");
                }
            } else {
                // Handle the case where no results were returned
                throw new InvalidOperationException("No results were returned from the Invoke-MgGraphRequest command.");
            }
        } catch (RuntimeException ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
            if (errorAction == ActionPreference.Stop) {
                throw;
            }
            if (!Suppress) {
                var result = new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message) {
                    GraphError = GraphApiErrorParser.Parse(ex.Message)
                };
                WriteObject(result);
            }
        }

        return "";
    }

    private static long GetTotalAttachmentSize(IEnumerable<GraphAttachment> attachments) {
        long size = 0;
        foreach (var a in attachments) {
            if (string.IsNullOrWhiteSpace(a.ContentBytes)) {
                continue;
            }
            try {
                size += Convert.FromBase64String(a.ContentBytes).LongLength;
            } catch (FormatException) {
            }
        }
        return size;
    }

    private object[]? FilterExistingPaths(object[]? paths, string parameterName) {
        if (paths == null) {
            return null;
        }

        List<object> valid = new();

        foreach (var item in paths) {
            string? path = item switch {
                string s => s,
                FileInfo fi => fi.FullName,
                _ => null
            };

            if (path != null) {
                if (path.IndexOfAny(new[] { '*', '?' }) >= 0) {
                    string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
                    string pattern = Path.GetFileName(path);
                    int startCount = valid.Count;
                    foreach (var file in Directory.GetFiles(directory, pattern)) {
                        if (File.Exists(file)) {
                            valid.Add(new FileInfo(file));
                        }
                    }
                    if (valid.Count == startCount) {
                        WriteWarning($"Send-EmailMessage - No files found for wildcard pattern: {path}. Removing from '{parameterName}'.");
                    }
                    continue;
                }

                if (!File.Exists(path)) {
                    WriteWarning($"Send-EmailMessage - File not found: {path}. Removing from '{parameterName}'.");
                    continue;
                }
            }

            if (item != null) {
                valid.Add(item);
            }
        }

        return valid.Count > 0 ? valid.ToArray() : null;
    }

    /// <summary>
    /// Cleans up logging resources after the cmdlet finishes executing.
    /// </summary>
    protected override void EndProcessing()
    {
        if (_logger != null) {
            if (_onVerbose != null) _logger.OnVerboseMessage -= _onVerbose;
            if (_onWarning != null) _logger.OnWarningMessage -= _onWarning;
            if (_onError != null) _logger.OnErrorMessage -= _onError;
            if (_onInformation != null) _logger.OnInformationMessage -= _onInformation;
        }
        _logCollector = null;
    }
}

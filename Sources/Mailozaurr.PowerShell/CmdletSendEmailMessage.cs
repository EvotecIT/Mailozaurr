namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommunications.Send, "EmailMessage", DefaultParameterSetName = "Compatibility", SupportsShouldProcess = true)]
[CmdletBinding()]
public sealed class CmdletSendEmailMessage : PSCmdlet {
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = true, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = true, ParameterSetName = "DefaultCredentials")]
    [Alias("SmtpServer")]
    public string Server { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    public int Port { get; set; } = 587;

    [Parameter(Mandatory = true, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = true, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = true, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = true, ParameterSetName = "EmailProviders")]
    public Object From { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public string ReplyTo { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public object[] Cc { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public object[] Bcc { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public object[] To { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public string Subject { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("Importance")]
    public MessagePriority Priority { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [ValidateSet("ASCII", "BigEndianUnicode", "Default", "Unicode", "UTF32", "UTF7", "UTF8")]
    public string Encoding { get; set; } = "Default";

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public Mailozaurr.DeliveryNotification[]? DeliveryNotificationOption { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public MailKit.Net.Smtp.DeliveryStatusNotificationType? DeliveryStatusNotificationType { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = true, ParameterSetName = "EmailProviders")]
    public PSCredential Credential { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public string Username { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public string Password { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public MailKit.Security.SecureSocketOptions SecureSocketOptions { get; set; } = MailKit.Security.SecureSocketOptions.Auto;

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public SwitchParameter UseSsl { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public SwitchParameter SkipCertificateRevocation { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Alias("SkipCertificateValidatation")]
    public SwitchParameter SkipCertificateValidation { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("Body", "HtmlBody")]
    public string[] HTML { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("TextBody")]
    public string[] Text { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("Attachments")]
    public string[]? Attachment { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public int Timeout { get; set; } = 12000;

    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Alias("oAuth")]
    public SwitchParameter OAuth2 { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter RequestReadReceipt { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter RequestDeliveryReceipt { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter Graph { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public SwitchParameter AsSecureString { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public SwitchParameter SendGrid { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public SwitchParameter SeparateTo { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter DoNotSaveToSentItems { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public SwitchParameter Suppress { get; set; }

    [Parameter(Mandatory = false)]
    public string LogPath { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter LogConsole { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter LogObject { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter LogTimestamps { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter LogSecrets { get; set; }

    [Parameter(Mandatory = false)]
    public string LogTimeStampsFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string LogServerPrefix { get; set; }

    [Parameter(Mandatory = false)]
    public string LogClientPrefix { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string MimeMessagePath { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string LocalDomain { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    public bool UseDefaultCredentials { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public EmailActionEncryption SignOrEncrypt { get; set; } = EmailActionEncryption.None;

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string CertificatePath { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string CertificatePassword { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public bool CertificatePasswordAsSecureString { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string CertificateThumbprint { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "EmailProviders")]
    public EmailProvider EmailProvider { get; set; }

    private ActionPreference errorAction;

    protected override void BeginProcessing() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        var internalLogger = new InternalLogger();
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = internalLogger;

        // Get the error action preference as user requested
        // It first sets the error action to the default error action preference
        // If the user has specified the error action, it will set the error action to the user specified error action
        errorAction = (ActionPreference)this.SessionState.PSVariable.GetValue("ErrorActionPreference");
        if (this.MyInvocation.BoundParameters.ContainsKey("ErrorAction")) {
            string errorActionString = this.MyInvocation.BoundParameters["ErrorAction"].ToString();
            if (Enum.TryParse(errorActionString, true, out ActionPreference actionPreference)) {
                errorAction = actionPreference;
            }
        }
    }

    protected override void ProcessRecord() {
        if (SendGrid || EmailProvider == EmailProvider.SendGrid) {
            SendGridClient sendGrid = new SendGridClient();
            sendGrid.From = From;
            if (Bcc != null) sendGrid.Bcc = Bcc.ToList();
            if (Cc != null) sendGrid.Cc = Cc.ToList();
            if (To != null) sendGrid.To = To.ToList();
            sendGrid.ReplyTo = ReplyTo;
            sendGrid.Subject = Subject;
            if (Text != null) sendGrid.Text = string.Join("", Text);
            if (HTML != null) sendGrid.Html = string.Join("", HTML);
            sendGrid.Priority = Priority;
            sendGrid.Attachment = Attachment;
            sendGrid.SeparateTo = SeparateTo;
            sendGrid.ErrorAction = errorAction;
            NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
            sendGrid.Credentials = networkCredential;
            // create JSON message
            sendGrid.CreateMessage();
            if (ShouldProcess(sendGrid.SentTo, "Sending email message via SendGrid")) {
                var result = sendGrid.SendEmailAsync().GetAwaiter().GetResult();
                if (!Suppress) {
                    WriteObject(result);
                }
            } else {
                if (!Suppress) {
                    WriteObject(new SmtpResult(false, EmailAction.Send, sendGrid.SentTo, sendGrid.SentFrom,
                        "SendGridApi", 0, sendGrid.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
                }
            }

        } else if (EmailProvider == EmailProvider.Mailgun) {
            NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);

            //MailgunClient mailgun = new MailgunClient(networkCredential, From, To, Cc, Bcc, Subject, Text, HTML);

        } else if (Graph) {
            Graph graph = new Graph();
            graph.From = From.ToString();
            graph.To = To;
            graph.Cc = Cc;
            graph.Bcc = Bcc;
            graph.ReplyTo = ReplyTo;
            graph.Subject = Subject;
            graph.DoNotSaveToSentItems = DoNotSaveToSentItems;
            graph.ErrorAction = errorAction;
            graph.RequestReadReceipt = RequestReadReceipt;
            graph.RequestDeliveryReceipt = RequestDeliveryReceipt;
            graph.HTML = string.Join("", HTML);
            graph.ContentType = "HTML";
            graph.Attachments = Attachment;
            graph.CreateAttachments();

            NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
            graph.Authenticate(networkCredential);
            var Status = graph.ConnectO365GraphAsync().GetAwaiter().GetResult();
            if (!Status.Status) {
                if (!Suppress) {
                    WriteObject(Status);
                }
                return;
            }
            if (graph.IsLargerAttachment) {
                Status = graph.SendMessageDraftAsync().GetAwaiter().GetResult();
            } else {
                Status = graph.SendMessageAsync().GetAwaiter().GetResult();
            }
            if (!Suppress) {
                WriteObject(Status);
            }
        } else if (MgGraphRequest) {
            Graph graph = new Graph();
            graph.From = From.ToString();
            graph.To = To;
            graph.Cc = Cc;
            graph.Bcc = Bcc;
            graph.ReplyTo = ReplyTo;
            graph.Subject = Subject;
            graph.DoNotSaveToSentItems = DoNotSaveToSentItems;
            graph.ErrorAction = errorAction;
            graph.RequestReadReceipt = RequestReadReceipt;
            graph.RequestDeliveryReceipt = RequestDeliveryReceipt;
            graph.HTML = string.Join("", HTML);
            graph.ContentType = "HTML";
            graph.Attachments = Attachment;
            graph.CreateAttachments();
            if (graph.IsLargerAttachment) {
                // create draft message
                var json = graph.CreateDraftForMg();
                var draftMessageId = InvokeMgGraphRequestPOST1($"v1.0/users/{graph.From}/mailfolders/drafts/messages", EmailAction.SendDraftMessage, json, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
                graph.PrepareAttachments().GetAwaiter().GetResult();
                // Upload attachments to the draft message
                foreach (var attachment in graph.AttachmentsPlaceHolders) {
                    var uploadUrl = InvokeMgGraphRequestPOST($"v1.0/users('{graph.SentFrom}')/messages/{draftMessageId}/attachments/createUploadSession", EmailAction.Send, attachment.Json, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
                    if (uploadUrl != "") {
                        InvokeMgGraphRequestPUT(uploadUrl, EmailAction.SendAttachment, attachment, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
                    } else {
                        LoggingMessages.Logger.WriteVerbose("Bro?");
                    }
                }
                InvokeMgGraphRequest($"https://graph.microsoft.com/v1.0/users('{graph.SentFrom}')/messages/{draftMessageId}/send", EmailAction.Send, graph.MessageJson, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
            } else {
                graph.CreateMessage();
                InvokeMgGraphRequest($"v1.0/users/{From}/sendMail", EmailAction.Send, graph.MessageJson, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
            }
        } else {
            Smtp SmtpClient = new Smtp(LogPath, LogConsole, LogObject, LogTimestamps, LogSecrets, LogTimeStampsFormat, LogClientPrefix, LogServerPrefix);
            SmtpClient.From = From;
            SmtpClient.ReplyTo = ReplyTo;
            SmtpClient.Cc = Cc;
            SmtpClient.Bcc = Bcc;
            SmtpClient.To = To;
            SmtpClient.Subject = Subject;
            SmtpClient.Priority = Priority;

            //SmtpClient.Encoding = Encoding;
            SmtpClient.DeliveryNotificationOption = DeliveryNotificationOption;
            SmtpClient.DeliveryStatusNotificationType = DeliveryStatusNotificationType;

            SmtpClient.CheckCertificateRevocation = !SkipCertificateRevocation;
            // SmtpClient.SkipCertificateValidation = SkipCertificateValidation;
            if (HTML != null) SmtpClient.HtmlBody = string.Join("", HTML);
            if (Text != null) SmtpClient.TextBody = string.Join("", Text);

            SmtpClient.Attachments = Attachment?.ToList();
            SmtpClient.Timeout = Timeout;

            SmtpClient.ErrorAction = errorAction;

            // Connect
            var Status = SmtpClient.Connect(Server, Port, SecureSocketOptions, UseSsl);
            if (!Status.Status) {
                if (!Suppress) {
                    WriteObject(Status);
                }

                SmtpClient.Dispose();
                return;
            }

            SmtpClient.CreateMessage();

            // Sign or Encrypt
            if (SignOrEncrypt != EmailActionEncryption.None) {
                if (CertificateThumbprint != null) {
                    Status = SmtpClient.Encrypt(SignOrEncrypt, CertificateThumbprint);
                } else if (CertificatePath != null && CertificatePassword != null) {
                    Status = SmtpClient.Encrypt(SignOrEncrypt, CertificatePath, CertificatePassword,
                        CertificatePasswordAsSecureString);
                }

                if (!Status.Status) {
                    if (!Suppress) {
                        WriteObject(Status);
                    }

                    SmtpClient.Dispose();
                    return;
                }
            }

            // Authenticate
            if (UseDefaultCredentials) {
                Status = SmtpClient.AuthenticateDefaultCredentials();
            } else if (Credential != null) {
                NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
                Status = SmtpClient.Authenticate(networkCredential, OAuth2);
            } else {
                Status = SmtpClient.Authenticate(Username, Password, AsSecureString);
            }

            if (!Status.Status) {
                if (!Suppress) {
                    WriteObject(Status);
                }

                SmtpClient.Dispose();
                return;
            }

            if (ShouldProcess(SmtpClient.SentTo, "Sending email message")) {
                // Send the message
                Status = SmtpClient.Send();
                if (!Suppress) {
                    WriteObject(Status);
                }

                // Save the message
                SmtpClient.SaveMessage(MimeMessagePath);
            } else {
                WriteObject(new SmtpResult(false, EmailAction.Send, SmtpClient.SentTo, SmtpClient.SentFrom, SmtpClient.Server, SmtpClient.Port, SmtpClient.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
            }

            // Disconnect & Dispose
            SmtpClient.Dispose();
        }
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
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
            if (errorAction == ActionPreference.Stop) {
                throw;
            }
            if (!Suppress) {
                WriteObject(new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message));
            }
        }
    }

    private void InvokeMgGraphRequestPUT(string uri, EmailAction action, GraphAttachmentPlaceHolder attachment, string sentFrom, string sentTo, TimeSpan elapsed) {
        foreach (var body in attachment.Content) {
            var parameters = new Hashtable {
                { "Method", "PUT" },
                { "Uri", uri },
                { "ContentType", "application/json; charset=UTF-8" },
                { "Body",  body.ReadAsByteArrayAsync().Result },
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
            } catch (Exception ex) {
                LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
                if (errorAction == ActionPreference.Stop) {
                    throw;
                }

                if (!Suppress) {
                    WriteObject(new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message));
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
                    return dictionary["uploadUrl"].ToString();
                } else {
                    // Handle the case where the property is not present
                    throw new InvalidOperationException("The result does not contain an 'uploadUrl' property.");
                }
            } else {
                // Handle the case where no results were returned
                throw new InvalidOperationException("No results were returned from the Invoke-MgGraphRequest command.");
            }
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
            if (errorAction == ActionPreference.Stop) {
                throw;
            }
            if (!Suppress) {
                WriteObject(new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message));
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
                    return dictionary["id"].ToString();
                } else {
                    // Handle the case where the property is not present
                    throw new InvalidOperationException("The result does not contain an 'id' property.");
                }
            } else {
                // Handle the case where no results were returned
                throw new InvalidOperationException("No results were returned from the Invoke-MgGraphRequest command.");
            }
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Send-EmailMessage - Error during sending using Graph Api (MgGraphRequest): {ex.Message}");
            if (errorAction == ActionPreference.Stop) {
                throw;
            }
            if (!Suppress) {
                WriteObject(new SmtpResult(false, action, sentTo, sentFrom, "GraphAPI", 0, elapsed, "", ex.Message));
            }
        }

        return "";
    }
}

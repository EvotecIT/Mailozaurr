namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommunications.Send, "EmailMessage", DefaultParameterSetName = "Compatibility", SupportsShouldProcess = true)]
[CmdletBinding()]
public sealed class CmdletSendEmailMessage : AsyncPSCmdlet {
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
    public Object From { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public string ReplyTo { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public object[] Cc { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public object[] Bcc { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public object[] To { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public string Subject { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
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
    public bool SkipCertificateValidation { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Alias("Body", "HtmlBody")]
    public string[] HTML { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Alias("TextBody")]
    public string[] Text { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Alias("Attachments")]
    public string[]? Attachment { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public int Timeout { get; set; } = 12000;

    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Alias("oAuth")]
    public bool OAuth2 { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public bool RequestReadReceipt { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public bool RequestDeliveryReceipt { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public bool Graph { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public bool MgGraphRequest { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public bool AsSecureString { get; set; }

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


    protected override Task BeginProcessingAsync() {
        // Initialize the logger to be able to see verbose, warning, debug, error, progress, and information messages.
        var internalLogger = new InternalLogger();
        var internalLoggerPowerShell = new InternalLoggerPowerShell(internalLogger, this.WriteVerbose, this.WriteWarning, this.WriteDebug, this.WriteError, this.WriteProgress, this.WriteInformation);
        LoggingMessages.Logger = internalLogger;
        return Task.CompletedTask;
    }

    protected override Task ProcessRecordAsync() {
        if (SendGrid) {
            SendGridClient sendGrid = new SendGridClient();
            NetworkCredential networkCredential = new NetworkCredential(Credential.UserName, Credential.Password);
            sendGrid.Credentials = networkCredential;
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
            // create JSON message
            sendGrid.CreateMessage();
            if (ShouldProcess(sendGrid.SentTo, "Sending email message via SendGrid")) {
                var result = sendGrid.SendEmailAsync().GetAwaiter().GetResult();
                if (!Suppress) {
                    WriteObject(result);
                }
            } else {
                if (!Suppress) {
                    WriteObject(new SmtpResult(false, EmailAction.Send, sendGrid.SentTo, sendGrid.SentFrom, "SendGridApi", 0, sendGrid.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
                }
            }

        } else if (Graph) {
            //SmtpClient.RequestReadReceipt = RequestReadReceipt;
            //SmtpClient.RequestDeliveryReceipt = RequestDeliveryReceipt;
            //SmtpClient.DoNotSaveToSentItems = DoNotSaveToSentItems;
        } else if (MgGraphRequest) {
            //SmtpClient.RequestReadReceipt = RequestReadReceipt;
            //SmtpClient.RequestDeliveryReceipt = RequestDeliveryReceipt;
            //SmtpClient.DoNotSaveToSentItems = DoNotSaveToSentItems;
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

            //ActionPreference errorAction = this.MyInvocation.BoundParameters.ContainsKey("ErrorAction")
            //    ? (ActionPreference)this.MyInvocation.BoundParameters["ErrorAction"]
            //    : ActionPreference.Continue;

            ActionPreference errorActionPreference = (ActionPreference)this.SessionState.PSVariable.GetValue("ErrorActionPreference");
            SmtpClient.ErrorAction = errorActionPreference;

            // Connect
            var Status = SmtpClient.Connect(Server, Port, SecureSocketOptions, UseSsl);
            if (!Status.Status) {
                if (!Suppress) {
                    WriteObject(Status);
                }

                SmtpClient.Dispose();
                return Task.CompletedTask;
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
                    return Task.CompletedTask;
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
                return Task.CompletedTask;
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
                WriteObject(new SmtpResult(false, EmailAction.Send, SmtpClient.SentTo, SmtpClient.SentFrom,
                    SmtpClient.Server, SmtpClient.Port, SmtpClient.Stopwatch.Elapsed, "", "Email not sent (WhatIf)"));
            }

            // Disconnect & Dispose
            SmtpClient.Dispose();
        }
        return Task.CompletedTask;
    }
}

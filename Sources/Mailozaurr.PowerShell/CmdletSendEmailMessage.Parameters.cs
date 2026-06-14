using Mailozaurr;
using System;
using System.IO;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr.PowerShell;

public sealed partial class CmdletSendEmailMessage : PSCmdlet {
    /// <summary>
    /// <para>Specifies the SMTP server to use for sending the email message. Required for SMTP scenarios.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = true, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = true, ParameterSetName = "DefaultCredentials")]
    [Alias("SmtpServer")]
    [ValidateNotNullOrEmpty]
    public string? Server { get; set; }

    /// <summary>
    /// <para>Specifies the port to use on the SMTP server. The default is 587.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    public int Port { get; set; } = 587;

    /// <summary>
    /// <para>Specifies the sender's email address. Can be a string or a hashtable with Name and Email keys.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = true, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = true, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = true, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = true, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = true, ParameterSetName = "EmailProviders")]
    [ValidateNotNullOrEmpty]
    public object? From { get; set; }

    /// <summary>
    /// <para>Specifies the reply-to address for the email. If not set, defaults to the From address.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// <para>Specifies the email addresses to which a carbon copy (CC) of the email message is sent.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public object[]? Cc { get; set; }

    /// <summary>
    /// <para>Specifies the email addresses that receive a blind carbon copy (BCC) of the email message.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public object[]? Bcc { get; set; }

    /// <summary>
    /// <para>Specifies the recipient email addresses. Accepts a single address or an array of addresses.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public object[]? To { get; set; }

    /// <summary>
    /// <para>Specifies the subject of the email message.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public string? Subject { get; set; }

    /// <summary>
    /// <para>Specifies the priority of the email message. Acceptable values are Normal, High, and Low.</para>
    /// </summary>
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

    /// <summary>
    /// <para>Specifies the encoding for the email message. Recommended to leave as default.</para>
    /// <para>Acceptable values: ASCII, BigEndianUnicode, Default, Unicode, UTF32, UTF7, UTF8.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [ValidateSet("ASCII", "BigEndianUnicode", "Default", "Unicode", "UTF32", "UTF7", "UTF8")]
    public string? Encoding { get; set; } = "Default";

    /// <summary>
    /// <para>Specifies the delivery notification options for the email message. Multiple options can be chosen.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public Mailozaurr.DeliveryNotification[]? DeliveryNotificationOption { get; set; }

    /// <summary>
    /// <para>Specifies the delivery status notification type. Options are Full, HeadersOnly, Unspecified.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public MailKit.Net.Smtp.DeliveryStatusNotificationType? DeliveryStatusNotificationType { get; set; }

    /// <summary>
    /// <para>Specifies a user account or API key/token for authentication. Used for SMTP, SendGrid, and Graph API.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = true, ParameterSetName = "EmailProviders")]
    [Parameter(Mandatory = true, ParameterSetName = "oAuth")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// <para>Specifies the username for SMTP authentication. Used with Password.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public string? Username { get; set; }

    /// <summary>
    /// <para>Specifies the password for SMTP authentication. Used with Username. Can be clear text or secure string.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public string? Password { get; set; }
    /// <summary>
    /// <para>Specifies the SASL mechanism for authentication. Defaults to Plain.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public AuthenticationMechanism AuthenticationMechanism { get; set; } = AuthenticationMechanism.Plain;
    /// <summary>
    /// <para>Specifies the secure socket options for SMTP connection. Options: None, Auto, StartTls, StartTlsWhenAvailable, SslOnConnect. Default is Auto.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public MailKit.Security.SecureSocketOptions SecureSocketOptions { get; set; } = MailKit.Security.SecureSocketOptions.Auto;

    /// <summary>
    /// <para>Enables the use of SSL/TLS for the SMTP connection. If
    /// <see cref="SecureSocketOptions"/> remains <c>Auto</c>, this switch causes
    /// <c>StartTls</c> to be used automatically.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public SwitchParameter UseSsl { get; set; }

    /// <summary>
    /// <para>Skips certificate revocation check during SMTP connection.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public SwitchParameter SkipCertificateRevocation { get; set; }

    /// <summary>
    /// <para>Skips certificate validation. Useful for self-signed certificates or IP-based SMTP servers.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Alias("SkipCertificateValidatation")]
    public SwitchParameter SkipCertificateValidation { get; set; }

    /// <summary>
    /// <para>Specifies the HTML body of the email message. Use for rich content emails. Alias: Body, HtmlBody.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("Body", "HtmlBody")]
    public string[]? HTML { get; set; }

    /// <summary>
    /// <para>Specifies the plain text body of the email message. Alias: TextBody.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("TextBody")]
    public string[]? Text { get; set; }

    /// <summary>
    /// <para>Specifies file paths to attach to the email message. Alias: Attachments.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    [Alias("Attachments")]
    public object[]? Attachment { get; set; }

    /// <summary>
    /// <para>Specifies inline attachments for the email message.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Alias("InlineAttachments")]
    public object[]? InlineAttachment { get; set; }

    /// <summary>
    /// Custom message headers to include with the email.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public Hashtable? Headers { get; set; }

    /// <summary>
    /// <para>Specifies the maximum time (in milliseconds) to wait for the SMTP operation to complete. Default is 12000.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public int Timeout { get; set; } = 12000;

    /// <summary>
    /// <para>Specifies how many times the cmdlet should retry sending the message when an error occurs. Default is 0 (no retries).</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// <para>Delay in milliseconds between retry attempts.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// <para>Multiplicative backoff applied to the retry delay. Value of 1 disables backoff.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public double RetryDelayBackoff { get; set; } = 1.0;

    /// <summary>
    /// <para>Maximum delay in milliseconds between retries. 0 disables capping. Applies to all providers.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public int MaxDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// <para>Jitter window in milliseconds added to each retry delay. 0 disables jitter. Applies to all providers.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public int JitterMilliseconds { get; set; } = 0;

    /// <summary>
    /// <para>When specified, retries are attempted regardless of the error
    /// type. Without this switch, only transient errors are retried.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter RetryAlways { get; set; }

    /// <summary>
    /// <para>Specifies the AWS region when using the SES provider.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public string? Region { get; set; }

    /// <summary>
    /// <para>Specifies the Gmail account when using the Gmail provider.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "EmailProviders")]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// <para>Enables reuse of SMTP connections via a connection pool.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter UseConnectionPool { get; set; }

    /// <summary>
    /// <para>Maximum number of connections to keep in the pool.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    [ValidateRange(1, int.MaxValue)]
    public int ConnectionPoolSize { get; set; } = 2;

    /// <summary>
    /// <para>Overrides Graph concurrency for this invocation. When set, caps parallel Graph HTTP requests.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public int GraphMaxConcurrency { get; set; } = 0;

    /// <summary>
    /// <para>Enables SMTP fallback when Graph ultimately fails. Configure fallback with <c>Set-MailozaurrSmtpFallback</c>.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    public SwitchParameter EnableSmtpFallback { get; set; }


    /// <summary>
    /// <para>Specifies chunk size in bytes used for Graph attachment uploads. Default is 4MB.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [ValidateRange(1, Mailozaurr.Graph.MaxChunkSize)]
    public int ChunkSize { get; set; } = Mailozaurr.Graph.MaxChunkSize;

    /// <summary>
    /// <para>Enables sending email via OAuth2 authentication for SMTP.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Alias("oAuth")]
    public SwitchParameter OAuth2 { get; set; }

    /// <summary>
    /// <para>Requests a read receipt for the email message (Graph API only).</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter RequestReadReceipt { get; set; }

    /// <summary>
    /// <para>Requests a delivery receipt for the email message (Graph API only).</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter RequestDeliveryReceipt { get; set; }

    /// <summary>
    /// <para>Enables sending email via Microsoft Graph API.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter Graph { get; set; }

    /// <summary>
    /// <para>Enables sending email via Microsoft Graph API using Invoke-MgGraphRequest (requires Connect-MgGraph authentication).</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>
    /// <para>Indicates that the provided password is a SecureString.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    public SwitchParameter AsSecureString { get; set; }

    /// <summary>
    /// <para>Enables sending email via SendGrid API.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public SwitchParameter SendGrid { get; set; }

    /// <summary>
    /// <para>Sends each recipient in the To field as a separate email (SendGrid only). BCC/CC are ignored in this mode.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public SwitchParameter SeparateTo { get; set; }

    /// <summary>
    /// <para>Prevents saving the email to Sent Items (Graph API only).</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter DoNotSaveToSentItems { get; set; }

    /// <summary>
    /// <para>Suppresses output of the summary object.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    [Parameter(Mandatory = false, ParameterSetName = "SendGrid")]
    public SwitchParameter Suppress { get; set; }

    /// <summary>
    /// <para>Specifies the path to save the communication log with the server.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? LogPath { get; set; }

    /// <summary>
    /// <para>Specifies the path used to persist sent message metadata (opt-in).</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? SentLogPath { get; set; }

    /// <summary>
    /// <para>Enables logging of communication with the server to the console.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter LogConsole { get; set; }

    /// <summary>
    /// <para>Enables logging of communication with the server to an object as a message property.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter LogObject { get; set; }

    /// <summary>
    /// <para>Enables timestamps in the log output.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter LogTimestamps { get; set; }

    /// <summary>
    /// <para>Includes secrets in the log output.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter LogSecrets { get; set; }

    /// <summary>
    /// <para>Specifies the format for timestamps in the log file.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? LogTimeStampsFormat { get; set; }

    /// <summary>
    /// <para>Sets the log prefix for the server.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? LogServerPrefix { get; set; }

    /// <summary>
    /// <para>Sets the log prefix for the client.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public string? LogClientPrefix { get; set; }

    /// <summary>
    /// <para>Overwrites the existing log file when using <c>-LogPath</c>.</para>
    /// </summary>
    [Parameter(Mandatory = false)]
    public SwitchParameter LogOverwrite { get; set; }

    /// <summary>
    /// <para>Saves the email message to a file for troubleshooting purposes.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? MimeMessagePath { get; set; }

    /// <summary>
    /// <para>Specifies the local domain name for the SMTP client.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? LocalDomain { get; set; }

    /// <summary>
    /// <para>Enables the use of default credentials for SMTP authentication.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// <para>Specifies whether to sign or encrypt the email message. Requires certificate parameters.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public EmailActionEncryption SignOrEncrypt { get; set; } = EmailActionEncryption.None;

    /// <summary>
    /// <para>Specifies the path to the certificate used for signing or encrypting the email.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? CertificatePath { get; set; }

    /// <summary>
    /// <para>Specifies the password for the certificate used in signing or encrypting the email.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// <para>Indicates that the certificate password is provided as a SecureString.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public bool CertificatePasswordAsSecureString { get; set; }

    /// <summary>
    /// <para>Specifies the thumbprint of the certificate used for signing or encrypting the email.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// <para>Provides a certificate object used for S/MIME operations.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    /// Path to the recipient's public key used for PGP operations.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? PublicKeyPath { get; set; }

    /// <summary>
    /// Path to the sender's private key used for PGP signing.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Password for the private key when required.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public string? PrivateKeyPassword { get; set; }

    /// <summary>
    /// Indicates that <see cref="PrivateKeyPassword"/> is provided as a secure string.
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "DefaultCredentials")]
    [Parameter(Mandatory = false, ParameterSetName = "SecureString")]
    [Parameter(Mandatory = false, ParameterSetName = "oAuth")]
    [Parameter(Mandatory = false, ParameterSetName = "Compatibility")]
    public bool PrivateKeyPasswordAsSecureString { get; set; }

    /// <summary>
    /// <para>Specifies the email provider to use (e.g., SendGrid, Mailgun, etc.).</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "EmailProviders")]
    public EmailProvider EmailProvider { get; set; }
}

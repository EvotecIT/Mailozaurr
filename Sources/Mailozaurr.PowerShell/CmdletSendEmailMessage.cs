using System;
using System.Management.Automation;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Sends an email message using SMTP, SendGrid, or Microsoft Graph from within PowerShell. Replaces the deprecated Send-MailMessage.</para>
/// <para type="description">The Send-EmailMessage cmdlet sends an email message using a variety of providers and authentication methods. It supports SMTP (with or without SSL/TLS), SendGrid API, and Microsoft Graph API. The cmdlet allows for rich email composition, including HTML and text bodies, attachments, delivery notifications, and advanced logging. It is designed as a modern, secure, and flexible replacement for Send-MailMessage.</para>
/// <para type="description">Authentication can be provided via credentials, OAuth2, or provider-specific tokens. The cmdlet supports multiple parameter sets for compatibility with different authentication and provider scenarios.</para>
/// </summary>
/// <example>
///   <summary>Send a basic email via SMTP with credentials</summary>
///   <prefix>PS&gt; </prefix>
///   <code>Send-EmailMessage -From @{ Name = 'John Doe'; Email = 'john.doe@example.com' } -To 'recipient@example.com' -Server 'smtp.office365.com' -Credential (Get-Credential) -HTML '<b>Hello</b>' -Subject 'Test Email'</code>
/// </example>
/// <example>
///   <summary>Send an email with an attachment and high priority</summary>
///   <prefix>PS&gt; </prefix>
///   <code>Send-EmailMessage -From 'john.doe@example.com' -To 'recipient@example.com' -Subject 'Report' -Body 'See attached.' -Attachment 'C:\Reports\report.pdf' -Priority High -Server 'smtp.office365.com' -Credential (Get-Credential)</code>
/// </example>
/// <example>
///   <summary>Send an email using SendGrid API</summary>
///   <prefix>PS&gt; </prefix>
///   <code>$cred = ConvertTo-SendGridCredential -ApiKey 'YOUR_SENDGRID_KEY'
/// Send-EmailMessage -From 'john.doe@example.com' -To 'recipient@example.com' -Subject 'SendGrid Test' -Body 'Hello from SendGrid' -SendGrid -Credential $cred</code>
/// </example>
/// <example>
///   <summary>Send an email using Microsoft Graph API</summary>
///   <prefix>PS&gt; </prefix>
///   <code>$cred = ConvertTo-GraphCredential -ClientID 'CLIENT_ID' -ClientSecret 'SECRET' -DirectoryID 'TENANT_ID'
/// Send-EmailMessage -From @{ Name = 'John Doe'; Email = 'john.doe@example.com' } -To 'recipient@example.com' -Credential $cred -HTML '<b>Hello</b>' -Subject 'Graph API Email' -Graph</code>
/// </example>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
[Cmdlet(VerbsCommunications.Send, "EmailMessage", DefaultParameterSetName = "Compatibility", SupportsShouldProcess = true)]
[CmdletBinding()]
public sealed class CmdletSendEmailMessage : PSCmdlet {
    private const long GraphAttachmentLimitBytes = 150000000;
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
    /// <para>Specifies chunk size in bytes used for Graph attachment uploads. Default is 9MB.</para>
    /// </summary>
    [Parameter(Mandatory = false, ParameterSetName = "Graph")]
    [Parameter(Mandatory = false, ParameterSetName = "MgGraphRequest")]
    public int ChunkSize { get; set; } = 9000000;

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

    private ActionPreference errorAction;

    /// <summary>
    /// Begin block
    /// </summary>
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

        Smtp.MaxPoolSize = ConnectionPoolSize;
        Smtp.PoolingEnabled = UseConnectionPool.IsPresent;
    }
    /// <summary>
    /// Process the record.
    /// </summary>
    protected override void ProcessRecord() {
        Attachment = FilterExistingPaths(Attachment, nameof(Attachment));
        InlineAttachment = FilterExistingPaths(InlineAttachment, nameof(InlineAttachment));
        var (fromEmail, fromName) = Helpers.GetEmailAndName(From);

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
            ProcessMgGraphRequest(fromEmail, fromName);
        } else {
            ProcessSmtp(fromEmail, fromName);
        }
    }

    private void ProcessSendGrid(string? fromEmail, string? fromName) {
        var logCollector = new LogCollector();
        SendGridClient sendGrid = new SendGridClient();
        sendGrid.LogCollector = logCollector;
        sendGrid.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) sendGrid.Bcc = Bcc.ToList();
        if (Cc != null) sendGrid.Cc = Cc.ToList();
        if (To != null) sendGrid.To = To.ToList();
        sendGrid.ReplyTo = ReplyTo;
        sendGrid.Subject = Subject;
        if (Text != null) sendGrid.Text = string.Join("", Text);
        if (HTML != null) sendGrid.Html = string.Join("", HTML);
        sendGrid.Priority = Priority;
        if (Attachment != null) {
            sendGrid.Attachment = Attachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (Headers != null) sendGrid.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
        sendGrid.SeparateTo = SeparateTo;
        sendGrid.ErrorAction = errorAction;
        sendGrid.RetryCount = RetryCount;
        sendGrid.RetryDelayMilliseconds = RetryDelayMilliseconds;
        sendGrid.RetryDelayBackoff = RetryDelayBackoff;
        sendGrid.RetryAlways = RetryAlways.IsPresent;
        NetworkCredential networkCredential = new NetworkCredential(Credential?.UserName, Credential?.Password);
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

    private void ProcessMailgun(string? fromEmail, string? fromName) {
        var logCollector = new LogCollector();
        using MailgunClient mailgun = new MailgunClient();
        mailgun.LogCollector = logCollector;
        mailgun.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) mailgun.Bcc = Bcc.ToList();
        if (Cc != null) mailgun.Cc = Cc.ToList();
        if (To != null) mailgun.To = To.ToList();
        mailgun.ReplyTo = ReplyTo;
        mailgun.Subject = Subject;
        if (Text != null) mailgun.Text = string.Join("", Text);
        if (HTML != null) mailgun.Html = string.Join("", HTML);
        if (Attachment != null) {
            mailgun.Attachment = Attachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (InlineAttachment != null) {
            mailgun.InlineAttachment = InlineAttachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (Headers != null) mailgun.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
        mailgun.ErrorAction = errorAction;
        mailgun.RetryCount = RetryCount;
        mailgun.RetryDelayMilliseconds = RetryDelayMilliseconds;
        mailgun.RetryDelayBackoff = RetryDelayBackoff;
        mailgun.RetryAlways = RetryAlways.IsPresent;
        NetworkCredential networkCredential = new NetworkCredential(Credential?.UserName, Credential?.Password);
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

    private void ProcessSes(string? fromEmail, string? fromName) {
        var logCollector = new LogCollector();
        using SesClient ses = new SesClient();
        ses.LogCollector = logCollector;
        ses.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) ses.Bcc = Bcc.ToList();
        if (Cc != null) ses.Cc = Cc.ToList();
        if (To != null) ses.To = To.ToList();
        ses.ReplyTo = ReplyTo;
        ses.Subject = Subject;
        if (Text != null) ses.Text = string.Join("", Text);
        if (HTML != null) ses.Html = string.Join("", HTML);
        if (Attachment != null) {
            ses.Attachment = Attachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (InlineAttachment != null) {
            ses.InlineAttachment = InlineAttachment.Select(a => a?.ToString() ?? string.Empty).ToArray();
        }
        if (Headers != null) ses.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
        ses.ErrorAction = errorAction;
        ses.RetryCount = RetryCount;
        ses.RetryDelayMilliseconds = RetryDelayMilliseconds;
        ses.RetryDelayBackoff = RetryDelayBackoff;
        ses.RetryAlways = RetryAlways.IsPresent;
        if (!string.IsNullOrEmpty(Region)) ses.Region = Region;
        NetworkCredential networkCredential = new NetworkCredential(Credential?.UserName, Credential?.Password);
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

    private void ProcessGmail(string? fromEmail, string? fromName) {
        var smtp = new Smtp();
        smtp.From = Helpers.GetFromObject(fromEmail, fromName);
        if (Bcc != null) smtp.Bcc = Bcc.ToList();
        if (Cc != null) smtp.Cc = Cc.ToList();
        if (To != null) smtp.To = To.ToList();
        smtp.ReplyTo = ReplyTo;
        smtp.Subject = Subject;
        if (Text != null) smtp.TextBody = string.Join("", Text);
        if (HTML != null) smtp.HtmlBody = string.Join("", HTML);
        smtp.Attachments = Attachment?.ToList();
        smtp.InlineAttachments = InlineAttachment?.ToList();
        smtp.Priority = Priority;
        smtp.CreateMessage();

        var net = Credential!.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };

        var client = new GmailApiClient(oauth);
        try {
            if (ShouldProcess(smtp.SentTo, "Sending email message via Gmail API")) {
                var msg = client.SendAsync(GmailAccount!, smtp.Message).GetAwaiter().GetResult();
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

    private void ProcessGraph(string? fromEmail, string? fromName) {
        using Graph graph = new Graph();
        graph.ChunkSize = ChunkSize;
        graph.From = Helpers.GetFromObject(fromEmail, fromName);
        graph.To = To;
        graph.Cc = Cc;
        graph.Bcc = Bcc;
        graph.ReplyTo = ReplyTo;
        graph.Subject = Subject;
        graph.DoNotSaveToSentItems = DoNotSaveToSentItems;
        graph.ErrorAction = errorAction;
        graph.RetryCount = RetryCount;
        graph.RetryDelayMilliseconds = RetryDelayMilliseconds;
        graph.RetryDelayBackoff = RetryDelayBackoff;
        graph.RetryAlways = RetryAlways.IsPresent;
        graph.RequestReadReceipt = RequestReadReceipt;
        graph.RequestDeliveryReceipt = RequestDeliveryReceipt;
        graph.HTML = string.Join("", HTML);
        graph.ContentType = "HTML";
        graph.Attachments = Attachment;
        if (Headers != null) graph.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
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

        NetworkCredential networkCredential = new NetworkCredential(Credential?.UserName, Credential?.Password);
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

    private void ProcessMgGraphRequest(string? fromEmail, string? fromName) {
        using Graph graph = new Graph();
        graph.ChunkSize = ChunkSize;
        graph.From = Helpers.GetFromObject(fromEmail, fromName);
        graph.To = To;
        graph.Cc = Cc;
        graph.Bcc = Bcc;
        graph.ReplyTo = ReplyTo;
        graph.Subject = Subject;
        graph.DoNotSaveToSentItems = DoNotSaveToSentItems;
        graph.ErrorAction = errorAction;
        graph.RetryCount = RetryCount;
        graph.RetryDelayMilliseconds = RetryDelayMilliseconds;
        graph.RetryDelayBackoff = RetryDelayBackoff;
        graph.RequestReadReceipt = RequestReadReceipt;
        graph.RequestDeliveryReceipt = RequestDeliveryReceipt;
        graph.HTML = string.Join("", HTML);
        graph.ContentType = "HTML";
        graph.Attachments = Attachment;
        if (Headers != null) graph.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
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
            graph.PrepareAttachments().GetAwaiter().GetResult();
            foreach (var attachment in graph.AttachmentsPlaceHolders) {
                var uploadUrl = InvokeMgGraphRequestPOST(attachment.Json, EmailAction.Send, attachment.Json, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
                if (uploadUrl != string.Empty) {
                    InvokeMgGraphRequestPUT(uploadUrl, EmailAction.SendAttachment, attachment, graph.SentFrom, graph.SentTo, graph.Stopwatch.Elapsed);
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

    private void ProcessSmtp(string? fromEmail, string? fromName) {
        Smtp smtpClient = new Smtp(LogPath, LogConsole, LogObject, LogTimestamps, LogSecrets, LogTimeStampsFormat, LogServerPrefix, LogClientPrefix, LogOverwrite);
        smtpClient.From = Helpers.GetFromObject(fromEmail, fromName);
        smtpClient.ReplyTo = ReplyTo;
        smtpClient.Cc = Cc;
        smtpClient.Bcc = Bcc;
        smtpClient.To = To;
        smtpClient.Subject = Subject;
        smtpClient.Priority = Priority;

        smtpClient.DeliveryNotificationOption = DeliveryNotificationOption;
        smtpClient.DeliveryStatusNotificationType = DeliveryStatusNotificationType;

        smtpClient.CheckCertificateRevocation = !SkipCertificateRevocation;
        smtpClient.SkipCertificateValidation = SkipCertificateValidation;
        if (HTML != null) smtpClient.HtmlBody = string.Join("", HTML);
        if (Text != null) smtpClient.TextBody = string.Join("", Text);

        smtpClient.Attachments = Attachment?.ToList();
        smtpClient.InlineAttachments = InlineAttachment?.ToList();
        if (Headers != null) smtpClient.Headers = Headers.Cast<DictionaryEntry>().ToDictionary(d => (string)d.Key, d => (string)d.Value);
        smtpClient.Timeout = Timeout;

        smtpClient.ErrorAction = errorAction;
        smtpClient.RetryCount = RetryCount;
        smtpClient.RetryDelayMilliseconds = RetryDelayMilliseconds;
        smtpClient.RetryDelayBackoff = RetryDelayBackoff;
        smtpClient.RetryAlways = RetryAlways.IsPresent;

        if (!ShouldProcess(smtpClient.SentTo, "Sending email message")) {
            LoggingMessages.Logger.WriteVerbose("Send-EmailMessage - Skipping authentication");
            if (!Suppress) {
                WriteObject(new SmtpResult(false, EmailAction.Send, smtpClient.SentTo, smtpClient.SentFrom, Server, Port, TimeSpan.Zero, string.Empty, "Email not sent (WhatIf)"));
            }
            return;
        }

        var status = smtpClient.Connect(Server, Port, SecureSocketOptions, UseSsl);
        if (!status.Status) {
            if (!Suppress) {
                WriteObject(status);
            }

            smtpClient.Dispose();
            return;
        }

        smtpClient.CreateMessage();

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
            status = smtpClient.Authenticate(Username, Password, AsSecureString, AuthenticationMechanism);
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

        smtpClient.SaveMessage(MimeMessagePath);

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
            } catch (RuntimeException ex) {
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
        } catch (RuntimeException ex) {
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
        } catch (RuntimeException ex) {
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
}

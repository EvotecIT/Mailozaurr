---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version: https://github.com/EvotecIT/MailoZaurr
schema: 2.0.0
---
# Send-EmailMessage
## SYNOPSIS
Sends an email message using SMTP, SendGrid, or Microsoft Graph from within PowerShell. Replaces the deprecated Send-MailMessage.

The Send-EmailMessage cmdlet sends an email message using a variety of providers and authentication methods. It supports SMTP (with or without SSL/TLS), SendGrid API, and Microsoft Graph API. The cmdlet allows for rich email composition, including HTML and text bodies, attachments, delivery notifications, and advanced logging. It is designed as a modern, secure, and flexible replacement for Send-MailMessage.

Authentication can be provided via credentials, OAuth2, or provider-specific tokens. The cmdlet supports multiple parameter sets for compatibility with different authentication and provider scenarios.

## SYNTAX
### Compatibility (Default)
```powershell
Send-EmailMessage -Server <string> -From <Object> [-Port <int>] [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-Encoding <string>] [-DeliveryNotificationOption <DeliveryNotification[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-InlineAttachment <Object[]>] [-Headers <hashtable>] [-Timeout <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-MimeMessagePath <string>] [-LocalDomain <string>] [-SignOrEncrypt <EmailActionEncryption>] [-CertificatePath <string>] [-CertificatePassword <string>] [-CertificatePasswordAsSecureString <bool>] [-CertificateThumbprint <string>] [-Certificate <X509Certificate2>] [-PublicKeyPath <string>] [-PrivateKeyPath <string>] [-PrivateKeyPassword <string>] [-PrivateKeyPasswordAsSecureString <bool>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### SecureString
```powershell
Send-EmailMessage -Server <string> -From <Object> [-Port <int>] [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-Encoding <string>] [-DeliveryNotificationOption <DeliveryNotification[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>] [-Username <string>] [-Password <string>] [-AuthenticationMechanism <AuthenticationMechanism>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-InlineAttachment <Object[]>] [-Headers <hashtable>] [-Timeout <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-AsSecureString] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-MimeMessagePath <string>] [-LocalDomain <string>] [-SignOrEncrypt <EmailActionEncryption>] [-CertificatePath <string>] [-CertificatePassword <string>] [-CertificatePasswordAsSecureString <bool>] [-CertificateThumbprint <string>] [-Certificate <X509Certificate2>] [-PublicKeyPath <string>] [-PrivateKeyPath <string>] [-PrivateKeyPassword <string>] [-PrivateKeyPasswordAsSecureString <bool>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### oAuth
```powershell
Send-EmailMessage -Server <string> -From <Object> -Credential <pscredential> [-Port <int>] [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-Encoding <string>] [-DeliveryNotificationOption <DeliveryNotification[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-InlineAttachment <Object[]>] [-Headers <hashtable>] [-Timeout <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-OAuth2] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-MimeMessagePath <string>] [-LocalDomain <string>] [-SignOrEncrypt <EmailActionEncryption>] [-CertificatePath <string>] [-CertificatePassword <string>] [-CertificatePasswordAsSecureString <bool>] [-CertificateThumbprint <string>] [-Certificate <X509Certificate2>] [-PublicKeyPath <string>] [-PrivateKeyPath <string>] [-PrivateKeyPassword <string>] [-PrivateKeyPasswordAsSecureString <bool>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### DefaultCredentials
```powershell
Send-EmailMessage -Server <string> -From <Object> [-Port <int>] [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-Encoding <string>] [-DeliveryNotificationOption <DeliveryNotification[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-SkipCertificateRevocation] [-SkipCertificateValidation] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-InlineAttachment <Object[]>] [-Headers <hashtable>] [-Timeout <int>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-MimeMessagePath <string>] [-LocalDomain <string>] [-UseDefaultCredentials <bool>] [-SignOrEncrypt <EmailActionEncryption>] [-CertificatePath <string>] [-CertificatePassword <string>] [-CertificatePasswordAsSecureString <bool>] [-CertificateThumbprint <string>] [-Certificate <X509Certificate2>] [-PublicKeyPath <string>] [-PrivateKeyPath <string>] [-PrivateKeyPassword <string>] [-PrivateKeyPasswordAsSecureString <bool>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Graph
```powershell
Send-EmailMessage -From <Object> -Credential <pscredential> [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-Headers <hashtable>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-GraphMaxConcurrency <int>] [-EnableSmtpFallback] [-ChunkSize <int>] [-RequestReadReceipt] [-RequestDeliveryReceipt] [-Graph] [-DoNotSaveToSentItems] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### MgGraphRequest
```powershell
Send-EmailMessage -From <Object> [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-Headers <hashtable>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-GraphMaxConcurrency <int>] [-ChunkSize <int>] [-RequestReadReceipt] [-RequestDeliveryReceipt] [-Graph] [-MgGraphRequest] [-DoNotSaveToSentItems] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### SendGrid
```powershell
Send-EmailMessage -From <Object> -Credential <pscredential> [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-Headers <hashtable>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-SendGrid] [-SeparateTo] [-Suppress] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### EmailProviders
```powershell
Send-EmailMessage -From <Object> -Credential <pscredential> -EmailProvider <EmailProvider> [-ReplyTo <string>] [-Cc <Object[]>] [-Bcc <Object[]>] [-To <Object[]>] [-Subject <string>] [-Priority <MessagePriority>] [-HTML <string[]>] [-Text <string[]>] [-Attachment <Object[]>] [-Headers <hashtable>] [-RetryCount <int>] [-RetryDelayMilliseconds <int>] [-RetryDelayBackoff <double>] [-MaxDelayMilliseconds <int>] [-JitterMilliseconds <int>] [-RetryAlways] [-Region <string>] [-GmailAccount <string>] [-UseConnectionPool] [-ConnectionPoolSize <int>] [-LogPath <string>] [-SentLogPath <string>] [-LogConsole] [-LogObject] [-LogTimestamps] [-LogSecrets] [-LogTimeStampsFormat <string>] [-LogServerPrefix <string>] [-LogClientPrefix <string>] [-LogOverwrite] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sends an email message using SMTP, SendGrid, or Microsoft Graph from within PowerShell. Replaces the deprecated Send-MailMessage.

The Send-EmailMessage cmdlet sends an email message using a variety of providers and authentication methods. It supports SMTP (with or without SSL/TLS), SendGrid API, and Microsoft Graph API. The cmdlet allows for rich email composition, including HTML and text bodies, attachments, delivery notifications, and advanced logging. It is designed as a modern, secure, and flexible replacement for Send-MailMessage.

Authentication can be provided via credentials, OAuth2, or provider-specific tokens. The cmdlet supports multiple parameter sets for compatibility with different authentication and provider scenarios.

## EXAMPLES

### EXAMPLE 1
```powershell
PS> Send-EmailMessage -From @{ Name = 'John Doe'; Email = 'john.doe@example.com' } -To 'recipient@example.com' -Server 'smtp.office365.com' -Credential (Get-Credential) -HTML 'Hello' -Subject 'Test Email'
```


### EXAMPLE 2
```powershell
PS> Send-EmailMessage -From 'john.doe@example.com' -To 'recipient@example.com' -Subject 'Report' -Body 'See attached.' -Attachment 'C:\Reports\report.pdf' -Priority High -Server 'smtp.office365.com' -Credential (Get-Credential)
```


### EXAMPLE 3
```powershell
PS> $cred = ConvertTo-SendGridCredential -SecretName 'sendgrid-api-key' -VaultName 'MailSecrets'
Send-EmailMessage -From 'john.doe@example.com' -To 'recipient@example.com' -Subject 'SendGrid Test' -Body 'Hello from SendGrid' -SendGrid -Credential $cred
```


### EXAMPLE 4
```powershell
PS> $cred = ConvertTo-GraphCredential -ClientID 'CLIENT_ID' -SecretName 'graph-client-secret' -VaultName 'MailSecrets' -DirectoryID 'TENANT_ID'
Send-EmailMessage -From @{ Name = 'John Doe'; Email = 'john.doe@example.com' } -To 'recipient@example.com' -Credential $cred -HTML 'Hello' -Subject 'Graph API Email' -Graph
```


## PARAMETERS

### -AsSecureString
Indicates that the provided password is a SecureString.

```yaml
Type: SwitchParameter
Parameter Sets: SecureString
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Attachment
Specifies file paths to attach to the email message. Alias: Attachments.

```yaml
Type: Object[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: Attachments
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AuthenticationMechanism
Specifies the SASL mechanism for authentication. Defaults to Plain.

```yaml
Type: AuthenticationMechanism
Parameter Sets: SecureString
Aliases: None
Possible values: Plain, Login, CramMd5

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Bcc
Specifies the email addresses that receive a blind carbon copy (BCC) of the email message.

```yaml
Type: Object[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Cc
Specifies the email addresses to which a carbon copy (CC) of the email message is sent.

```yaml
Type: Object[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Certificate
Provides a certificate object used for S/MIME operations.

```yaml
Type: X509Certificate2
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePassword
Specifies the password for the certificate used in signing or encrypting the email.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePasswordAsSecureString
Indicates that the certificate password is provided as a SecureString.

```yaml
Type: Boolean
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificatePath
Specifies the path to the certificate used for signing or encrypting the email.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CertificateThumbprint
Specifies the thumbprint of the certificate used for signing or encrypting the email.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ChunkSize
Specifies chunk size in bytes used for Graph attachment uploads. Default is 4MB.

```yaml
Type: Int32
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ConnectionPoolSize
Maximum number of connections to keep in the pool.

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Specifies a user account or API key/token for authentication. Used for SMTP, SendGrid, and Graph API.

```yaml
Type: PSCredential
Parameter Sets: oAuth, Graph, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryNotificationOption
Specifies the delivery notification options for the email message. Multiple options can be chosen.

```yaml
Type: DeliveryNotification[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values: None, Delay, Never, OnFailure, OnSuccess

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryStatusNotificationType
Specifies the delivery status notification type. Options are Full, HeadersOnly, Unspecified.

```yaml
Type: Nullable`1
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DoNotSaveToSentItems
Prevents saving the email to Sent Items (Graph API only).

```yaml
Type: SwitchParameter
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EmailProvider
Specifies the email provider to use (e.g., SendGrid, Mailgun, etc.).

```yaml
Type: EmailProvider
Parameter Sets: EmailProviders
Aliases: None
Possible values: None, SendGrid, Mailgun, SES, Gmail, Graph

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EnableSmtpFallback
Enables SMTP fallback when Graph ultimately fails. Requires a configured factory via [Mailozaurr.MailozaurrOptions]::SmtpFallbackFactory.

```yaml
Type: SwitchParameter
Parameter Sets: Graph
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encoding
Specifies the encoding for the email message. Recommended to leave as default.

Acceptable values: ASCII, BigEndianUnicode, Default, Unicode, UTF32, UTF7, UTF8.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values: ASCII, BigEndianUnicode, Default, Unicode, UTF32, UTF7, UTF8

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -From
Specifies the sender's email address. Can be a string or a hashtable with Name and Email keys.

```yaml
Type: Object
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GmailAccount
Specifies the Gmail account when using the Gmail provider.

```yaml
Type: String
Parameter Sets: EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Graph
Enables sending email via Microsoft Graph API.

```yaml
Type: SwitchParameter
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -GraphMaxConcurrency
Overrides Graph concurrency for this invocation. When set, caps parallel Graph HTTP requests.

```yaml
Type: Int32
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Headers
Custom message headers to include with the email.

```yaml
Type: Hashtable
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HTML
Specifies the HTML body of the email message. Use for rich content emails. Alias: Body, HtmlBody.

```yaml
Type: String[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: Body, HtmlBody
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InlineAttachment
Specifies inline attachments for the email message.

```yaml
Type: Object[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: InlineAttachments
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -JitterMilliseconds
Jitter window in milliseconds added to each retry delay. 0 disables jitter. Applies to all providers.

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LocalDomain
Specifies the local domain name for the SMTP client.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogClientPrefix
Sets the log prefix for the client.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogConsole
Enables logging of communication with the server to the console.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogObject
Enables logging of communication with the server to an object as a message property.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogOverwrite
Overwrites the existing log file when using -LogPath.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogPath
Specifies the path to save the communication log with the server.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogSecrets
Includes secrets in the log output.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogServerPrefix
Sets the log prefix for the server.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogTimestamps
Enables timestamps in the log output.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LogTimeStampsFormat
Specifies the format for timestamps in the log file.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MaxDelayMilliseconds
Maximum delay in milliseconds between retries. 0 disables capping. Applies to all providers.

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MgGraphRequest
Enables sending email via Microsoft Graph API using Invoke-MgGraphRequest (requires Connect-MgGraph authentication).

```yaml
Type: SwitchParameter
Parameter Sets: MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MimeMessagePath
Saves the email message to a file for troubleshooting purposes.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OAuth2
Enables sending email via OAuth2 authentication for SMTP.

```yaml
Type: SwitchParameter
Parameter Sets: oAuth
Aliases: oAuth
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
Specifies the password for SMTP authentication. Used with Username. Can be clear text or secure string.

```yaml
Type: String
Parameter Sets: SecureString
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Specifies the port to use on the SMTP server. The default is 587.

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Specifies the priority of the email message. Acceptable values are Normal, High, and Low.

```yaml
Type: MessagePriority
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: Importance
Possible values: High, Low, Normal

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PrivateKeyPassword
Password for the private key when required.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PrivateKeyPasswordAsSecureString
Indicates that PrivateKeyPassword is provided as a secure string.

```yaml
Type: Boolean
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PrivateKeyPath
Path to the sender's private key used for PGP signing.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PublicKeyPath
Path to the recipient's public key used for PGP operations.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Region
Specifies the AWS region when using the SES provider.

```yaml
Type: String
Parameter Sets: EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplyTo
Specifies the reply-to address for the email. If not set, defaults to the From address.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestDeliveryReceipt
Requests a delivery receipt for the email message (Graph API only).

```yaml
Type: SwitchParameter
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequestReadReceipt
Requests a read receipt for the email message (Graph API only).

```yaml
Type: SwitchParameter
Parameter Sets: Graph, MgGraphRequest
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryAlways
When specified, retries are attempted regardless of the error
type. Without this switch, only transient errors are retried.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryCount
Specifies how many times the cmdlet should retry sending the message when an error occurs. Default is 0 (no retries).

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayBackoff
Multiplicative backoff applied to the retry delay. Value of 1 disables backoff.

```yaml
Type: Double
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RetryDelayMilliseconds
Delay in milliseconds between retry attempts.

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecureSocketOptions
Specifies the secure socket options for SMTP connection. Options: None, Auto, StartTls, StartTlsWhenAvailable, SslOnConnect. Default is Auto.

```yaml
Type: SecureSocketOptions
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SendGrid
Enables sending email via SendGrid API.

```yaml
Type: SwitchParameter
Parameter Sets: SendGrid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SentLogPath
Specifies the path used to persist sent message metadata (opt-in).

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SeparateTo
Sends each recipient in the To field as a separate email (SendGrid only). BCC/CC are ignored in this mode.

```yaml
Type: SwitchParameter
Parameter Sets: SendGrid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Server
Specifies the SMTP server to use for sending the email message. Required for SMTP scenarios.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: SmtpServer
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SignOrEncrypt
Specifies whether to sign or encrypt the email message. Requires certificate parameters.

```yaml
Type: EmailActionEncryption
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values: None, SMIMESign, SMIMESignPkcs7, SMIMEEncrypt, SMIMESignAndEncrypt, PGPSign, PGPEncrypt, PGPSignAndEncrypt

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipCertificateRevocation
Skips certificate revocation check during SMTP connection.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipCertificateValidation
Skips certificate validation. Useful for self-signed certificates or IP-based SMTP servers.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: SkipCertificateValidatation
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subject
Specifies the subject of the email message.

```yaml
Type: String
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Suppress
Suppresses output of the summary object.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Text
Specifies the plain text body of the email message. Alias: TextBody.

```yaml
Type: String[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: TextBody
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Specifies the maximum time (in milliseconds) to wait for the SMTP operation to complete. Default is 12000.

```yaml
Type: Int32
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -To
Specifies the recipient email addresses. Accepts a single address or an array of addresses.

```yaml
Type: Object[]
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseConnectionPool
Enables reuse of SMTP connections via a connection pool.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials, Graph, MgGraphRequest, SendGrid, EmailProviders
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseDefaultCredentials
Enables the use of default credentials for SMTP authentication.

```yaml
Type: Boolean
Parameter Sets: DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Username
Specifies the username for SMTP authentication. Used with Password.

```yaml
Type: String
Parameter Sets: SecureString
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseSsl
Enables the use of SSL/TLS for the SMTP connection. If
SecureSocketOptions remains Auto, this switch causes
StartTls to be used automatically.

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, SecureString, oAuth, DefaultCredentials
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `None`

## RELATED LINKS

- [Mailozaurr Documentation](https://github.com/EvotecIT/Mailozaurr)

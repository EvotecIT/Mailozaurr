---
external help file: Mailozaurr-help.xml
Module Name: Mailozaurr
online version:
schema: 2.0.0
---

# Send-EmailMessage

## SYNOPSIS
The Send-EmailMessage cmdlet sends an email message from within PowerShell.

## SYNTAX

### Compatibility (Default)
```
Send-EmailMessage [-Server <String>] [-Port <Int32>] [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>]
 [-Bcc <String[]>] [-To <String[]>] [-Subject <String>] [-Priority <String>] [-Encoding <String>]
 [-DeliveryNotificationOption <String[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>]
 [-Credential <PSCredential>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-HTML <String[]>]
 [-Text <String[]>] [-Attachment <String[]>] [-Timeout <Int32>] [-Suppress] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### oAuth
```
Send-EmailMessage [-Server <String>] [-Port <Int32>] [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>]
 [-Bcc <String[]>] [-To <String[]>] [-Subject <String>] [-Priority <String>] [-Encoding <String>]
 [-DeliveryNotificationOption <String[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>]
 [-Credential <PSCredential>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl] [-HTML <String[]>]
 [-Text <String[]>] [-Attachment <String[]>] [-Timeout <Int32>] [-oAuth2] [-Suppress] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ClearText
```
Send-EmailMessage [-Server <String>] [-Port <Int32>] [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>]
 [-Bcc <String[]>] [-To <String[]>] [-Subject <String>] [-Priority <String>] [-Encoding <String>]
 [-DeliveryNotificationOption <String[]>] [-DeliveryStatusNotificationType <DeliveryStatusNotificationType>]
 [-Username <String>] [-Password <String>] [-SecureSocketOptions <SecureSocketOptions>] [-UseSsl]
 [-HTML <String[]>] [-Text <String[]>] [-Attachment <String[]>] [-Timeout <Int32>] [-Suppress] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

### SendGrid
```
Send-EmailMessage [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>] [-Bcc <String[]>] [-To <String[]>]
 [-Subject <String>] [-Priority <String>] -Credential <PSCredential> [-HTML <String[]>] [-Text <String[]>]
 [-Attachment <String[]>] [-SendGrid] [-SeparateTo] [-Suppress] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Graph
```
Send-EmailMessage [-From <Object>] [-ReplyTo <String>] [-Cc <String[]>] [-Bcc <String[]>] [-To <String[]>]
 [-Subject <String>] [-Priority <String>] -Credential <PSCredential> [-HTML <String[]>] [-Text <String[]>]
 [-Attachment <String[]>] [-Graph] [-DoNotSaveToSentItems] [-Suppress] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### Grouped
```
Send-EmailMessage [-Email <IDictionary>] [-Suppress] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
The Send-EmailMessage cmdlet sends an email message from within PowerShell.
It replaces Send-MailMessage by Microsoft which is deprecated.

## EXAMPLES

### EXAMPLE 1
```
if (-not $MailCredentials) {
    $MailCredentials = Get-Credential
}

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test.pl' } -To 'przemyslaw.klys@test.pl' \`
    -Server 'smtp.office365.com' -SecureSocketOptions Auto -Credential $MailCredentials -HTML $Body -DeliveryNotificationOption OnSuccess -Priority High \`
    -Subject 'This is another test email'
```

### EXAMPLE 2
```
if (-not $MailCredentials) {
    $MailCredentials = Get-Credential
}
# this is simple replacement (drag & drop to Send-MailMessage)
Send-EmailMessage -To 'przemyslaw.klys@test.pl' -Subject 'Test' -Body 'test me' -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@test.pl' \`
    -Attachments "$PSScriptRoot\README.MD" -Cc 'przemyslaw.klys@test.pl' -Priority High -Credential $MailCredentials \`
    -UseSsl -Port 587 -Verbose
```

### EXAMPLE 3
```
# Use SendGrid Api
$Credential = ConvertTo-SendGridCredential -ApiKey 'YourKey'

Send-EmailMessage -From 'przemyslaw.klys@evo.cool' \`
    -To 'przemyslaw.klys@evotec.pl', 'evotectest@gmail.com' \`
    -Body 'test me Przemysław Kłys' \`
    -Priority High \`
    -Subject 'This is another test email' \`
    -SendGrid \`
    -Credential $Credential \`
    -Verbose
```

### EXAMPLE 4
```
# It seems larger HTML is not supported. Online makes sure it uses less libraries inline
# it may be related to not escaping chars properly for JSON, may require investigation
$Body = EmailBody {
    EmailText -Text 'This is my text'
    EmailTable -DataTable (Get-Process | Select-Object -First 5 -Property Name, Id, PriorityClass, CPU, Product)
} -Online

# Credentials for Graph
$ClientID = '0fb383f1'
$DirectoryID = 'ceb371f6'
$ClientSecret = 'VKDM_'

$Credential = ConvertTo-GraphCredential -ClientID $ClientID -ClientSecret $ClientSecret -DirectoryID $DirectoryID

# Sending email
Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test1.pl' } -To 'przemyslaw.klys@test.pl' \`
    -Credential $Credential -HTML $Body -Subject 'This is another test email 1' -Graph -Verbose -Priority High
```

### EXAMPLE 5
```
# Using OAuth2 for Office 365
$ClientID = '4c1197dd-53'
$TenantID = 'ceb371f6-87'

$CredentialOAuth2 = Connect-oAuthO365 -ClientID $ClientID -TenantID $TenantID

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'test@evotec.pl' } -To 'test@evotec.pl' \`
    -Server 'smtp.office365.com' -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High \`
    -Subject 'This is another test email' -SecureSocketOptions Auto -Credential $CredentialOAuth2 -oAuth2
```

## PARAMETERS

### -Server
Specifies the name of the SMTP server that sends the email message.

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText
Aliases: SmtpServer

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Port
Specifies an alternate port on the SMTP server.
The default value is 587.

```yaml
Type: Int32
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: 587
Accept pipeline input: False
Accept wildcard characters: False
```

### -From
This parameter specifies the sender's email address.

```yaml
Type: Object
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReplyTo
This property indicates the reply address.
If you don't set this property, the Reply address is same as From address.

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Cc
Specifies the email addresses to which a carbon copy (CC) of the email message is sent.

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Bcc
Specifies the email addresses that receive a copy of the mail but are not listed as recipients of the message.

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -To
Specifies the recipient's email address.
If there are multiple recipients, separate their addresses with a comma (,)

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Subject
The Subject parameter isn't required.
This parameter specifies the subject of the email message.

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Priority
Specifies the priority of the email message.
Normal is the default.
The acceptable values for this parameter are Normal, High, and Low.

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases: Importance

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Encoding
Specifies the type of encoding for the target file.
It's recommended to not change it.

The acceptable values for this parameter are as follows:

default:
ascii: Uses the encoding for the ASCII (7-bit) character set.
bigendianunicode: Encodes in UTF-16 format using the big-endian byte order.
oem: Uses the default encoding for MS-DOS and console programs.
unicode: Encodes in UTF-16 format using the little-endian byte order.
utf7: Encodes in UTF-7 format.
utf8: Encodes in UTF-8 format.
utf32: Encodes in UTF-32 format.

```yaml
Type: String
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: Default
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryNotificationOption
Specifies the delivery notification options for the email message.
You can specify multiple values.
None is the default value.
The alias for this parameter is DNO.
The delivery notifications are sent to the address in the From parameter.
Multiple options can be chosen.

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeliveryStatusNotificationType
Specifies delivery status notification type.
Options are Full, HeadersOnly, Unspecified

```yaml
Type: DeliveryStatusNotificationType
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:
Accepted values: Unspecified, Full, HeadersOnly

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Credential
Specifies a user account that has permission to perform this action.
The default is the current user.
Type a user name, such as User01 or Domain01\User01.
Or, enter a PSCredential object, such as one from the Get-Credential cmdlet.
Credentials are stored in a PSCredential object and the password is stored as a SecureString.

Credential parameter is also use to securely pass tokens/api keys for Graph API/oAuth2/SendGrid

```yaml
Type: PSCredential
Parameter Sets: Compatibility, oAuth
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

```yaml
Type: PSCredential
Parameter Sets: SendGrid, Graph
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Username
Specifies UserName to use to login to server

```yaml
Type: String
Parameter Sets: ClearText
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Password
Specifies Password to use to login to server.
This is ClearText option and should not be used.

```yaml
Type: String
Parameter Sets: ClearText
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SecureSocketOptions
Specifies secure socket option: None, Auto, StartTls, StartTlsWhenAvailable, SslOnConnect.
Default is Auto.

```yaml
Type: SecureSocketOptions
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:
Accepted values: None, Auto, SslOnConnect, StartTls, StartTlsWhenAvailable

Required: False
Position: Named
Default value: Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### -UseSsl
Specifies using StartTLS option.
It's recommended to leave it disabled and use SecureSocketOptions which should take care of all security needs

```yaml
Type: SwitchParameter
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -HTML
HTML content to send email

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases: Body

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Text
Text content to send email.
With SMTP one can define both HTML and Text.
For SendGrid and Office 365 Graph API only HTML or Text will be used with HTML having priority

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Attachment
Specifies the path and file names of files to be attached to the email message.

```yaml
Type: String[]
Parameter Sets: Compatibility, oAuth, ClearText, SendGrid, Graph
Aliases: Attachments

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Timeout
Maximum time to wait to send an email via SMTP

```yaml
Type: Int32
Parameter Sets: Compatibility, oAuth, ClearText
Aliases:

Required: False
Position: Named
Default value: 12000
Accept pipeline input: False
Accept wildcard characters: False
```

### -oAuth2
Send email via oAuth2

```yaml
Type: SwitchParameter
Parameter Sets: oAuth
Aliases: oAuth

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Graph
Send email via Office 365 Graph API

```yaml
Type: SwitchParameter
Parameter Sets: Graph
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -SendGrid
Send email via SendGrid API

```yaml
Type: SwitchParameter
Parameter Sets: SendGrid
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -SeparateTo
Option separates each To field into separate emails (sent as one query).
Supported by SendGrid only!
BCC/CC are skipped when this mode is used.

```yaml
Type: SwitchParameter
Parameter Sets: SendGrid
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -DoNotSaveToSentItems
Do not save email to SentItems when sending with Office 365 Graph API

```yaml
Type: SwitchParameter
Parameter Sets: Graph
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -Email
Compatibility parameter for Send-Email cmdlet from PSSharedGoods

```yaml
Type: IDictionary
Parameter Sets: Grouped
Aliases: EmailParameters

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Suppress
Do not display summary in \[PSCustomObject\]

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES
General notes

## RELATED LINKS

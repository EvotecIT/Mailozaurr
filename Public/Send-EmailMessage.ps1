function Send-EmailMessage {
    <#
    .SYNOPSIS
    Short description

    .DESCRIPTION
    Long description

    .PARAMETER Server
    Parameter description

    .PARAMETER Port
    Parameter description

    .PARAMETER From
    Parameter description

    .PARAMETER ReplyTo
    Parameter description

    .PARAMETER Bcc
    Parameter description

    .PARAMETER To
    Parameter description

    .PARAMETER Subject
    Parameter description

    .PARAMETER Priority
    Parameter description

    .PARAMETER Encoding
    Parameter description

    .PARAMETER DeliveryStatusNotification

    +---------------+----------------------------------------------------+
    | Member Name   | Description                                        |
    +---------------+----------------------------------------------------+
    | Delay         | Notify if the delivery is delayed.                 |
    +---------------+----------------------------------------------------+
    | Never         | A notification should not be generated under       |
    |               | any circumstances.                                 |
    +---------------+----------------------------------------------------+
    | None          | No notification information will be sent. The mail |
    |               | server will utilize its configured behavior to     |
    |               | determine whether it should generate a delivery    |
    |               | notification.                                      |
    +---------------+----------------------------------------------------+
    | Failure       | Notify if the delivery is unsuccessful.            |
    +-------------+----------------------------------------------------+
    | Succes  s     | Notify if the delivery is successful               |
    +---------------+----------------------------------------------------+

    .PARAMETER Credentials
    Parameter description

    .PARAMETER SecureSocketOptions
    Parameter description

    .PARAMETER UseSsl
    Parameter description

    .PARAMETER HTML
    Parameter description

    .PARAMETER Text
    Parameter description

    .PARAMETER Attachments
    Parameter description

    .PARAMETER ShowErrors
    Parameter description

    .PARAMETER Suppress
    Parameter description

    .PARAMETER Email
    Parameter description

    .EXAMPLE
    An example

    .NOTES
    General notes
    #>
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][alias('SmtpServer')][string] $Server,
        [int] $Port = 587,
        [Parameter(Mandatory)][object] $From,
        [string] $ReplyTo,
        [Array] $Cc,
        [Array] $Bcc,
        [Array] $To,
        [string] $Subject,
        [ValidateSet('Low', 'Normal', 'High')][string] $Priority,
        [ValidateSet('ASCII', 'BigEndianUnicode', 'Default', 'Unicode', 'UTF32', 'UTF7', 'UTF8')][string] $Encoding = 'Default',
        [ValidateSet('None', 'OnSuccess', 'OnFailure', 'Delay', 'Never')][string[]] $DeliveryNotificationOption,
        [MailKit.Net.Smtp.DeliveryStatusNotificationType] $DeliveryStatusNotificationType,
        [pscredential] $Credential,
        [MailKit.Security.SecureSocketOptions] $SecureSocketOptions = [MailKit.Security.SecureSocketOptions]::Auto,
        [switch] $UseSsl,
        [alias('Body')][string[]] $HTML,
        [string[]] $Text,
        [alias('Attachments')][string[]] $Attachment,
        [switch] $ShowErrors,
        [switch] $Suppress,
        [alias('EmailParameters')][System.Collections.IDictionary] $Email
    )
    if ($Email) {
        <#

        $From = $Email.From
        $To = $Email.To
        EmailCC                     = $Email.CC
        $Bcc = $Email.BCC
        $ReplyTo = $Email.ReplyTo
        $Server = $Email.Server
        EmailServerPassword         = $Email.Password
        EmailServerPasswordAsSecure = $Email.PasswordAsSecure
        EmailServerPasswordFromFile = $Email.PasswordFromFile
        EmailServerPort             = $Email.Port
        EmailServerLogin            = $Email.Login
        $UseSsl = $Email.EnableSsl
        EmailEncoding               = $Email.Encoding
        EmailEncodingSubject        = $Email.EncodingSubject
        EmailEncodingBody           = $Email.EncodingBody
        $Subject = $Email.Subject
        EmailPriority               = $Email.Priority
        EmailDeliveryNotifications  = $Email.DeliveryNotifications
        EmailUseDefaultCredentials  = $Email.UseDefaultCredentials
        #>

    } else {
        if ($null -eq $To -and $null -eq $Bcc -and $null -eq $Cc) {
            Write-Warning "Send-EmailMessage - At least one To, CC or BCC is required."
            return
        }
    }
    if ($Credential) {
        $SmtpCredentials = $Credential
    }
    $Message = [MimeKit.MimeMessage]::new()

    # Doing translation for compatibility with Send-MailMessage
    if ($Priority -eq 'High') {
        $Message.Priority = [MimeKit.MessagePriority]::Urgent
    } elseif ($Priority -eq 'Low') {
        $Message.Priority = [MimeKit.MessagePriority]::NonUrgent
    } else {
        $Message.Priority = [MimeKit.MessagePriority]::Normal
    }

    [MimeKit.InternetAddress] $SmtpFrom = ConvertTo-MailboxAddress -MailboxAddress $From
    $Message.From.Add($SmtpFrom)

    if ($To) {
        [MimeKit.InternetAddress[]] $SmtpTo = ConvertTo-MailboxAddress -MailboxAddress $To
        $Message.To.AddRange($SmtpTo)
    }
    if ($Cc) {
        [MimeKit.InternetAddress[]] $SmtpCC = ConvertTo-MailboxAddress -MailboxAddress $Cc
        $Message.Cc.AddRange($SmtpCC)
    }
    if ($Bcc) {
        [MimeKit.InternetAddress[]] $SmtpBcc = ConvertTo-MailboxAddress -MailboxAddress $Bcc
        $Message.Bcc.AddRange($SmtpBcc)
    }
    if ($ReplyTo) {
        [MimeKit.InternetAddress] $SmtpReplyTo = ConvertTo-MailboxAddress -MailboxAddress $ReplyTo
        $Message.ReplyTo.Add($SmtpReplyTo)
    }
    $MailSentTo = -join ($To -join ',', $CC -join ', ', $Bcc -join ', ')
    if ($Subject) {
        $Message.Subject = $Subject
    }


    $BodyBuilder = [MimeKit.BodyBuilder]::new()
    <#
        MimeKit.TextPart new(MimeKit.MimeEntityConstructorArgs args)
        MimeKit.TextPart new(string subtype, Params System.Object[] args)
        MimeKit.TextPart new(string subtype)
        MimeKit.TextPart new(MimeKit.Text.TextFormat format)
        MimeKit.TextPart new()

        var message = new MimeMessage();
        message.Body = new TextPart ("html") { Text = "<b>Test Message</b>" };
        #>
    if ($HTML) {
        $BodyBuilder.HtmlBody = $HTML
    }
    if ($Text) {
        $BodyBuilder.TextBody = $Text
    }
    if ($Attachment) {
        foreach ($A in $Attachment) {
            $null = $BodyBuilder.Attachments.Add($A)
        }
    }
    $Message.Body = $BodyBuilder.ToMessageBody()

    ### SMTP Part Below

    class MySmtpClient : MailKit.Net.Smtp.SmtpClient {
        MySmtpClient() : base() {}

        [string[]] $DeliveryNotificationOption = @()
        [Nullable[MailKit.DeliveryStatusNotification]] GetDeliveryStatusNotifications([MimeKit.MimeMessage]$message, [MimeKit.MailboxAddress]$mailbox) {
            $Output = @(
                if ($this.DeliveryNotificationOption -contains 'OnSuccess') {
                    [MailKit.DeliveryStatusNotification]::Success
                }
                if ($this.DeliveryNotificationOption -contains 'Delay') {
                    [MailKit.DeliveryStatusNotification]::Delay
                }
                if ($this.DeliveryNotificationOption -contains 'OnFailure') {
                    [MailKit.DeliveryStatusNotification]::Failure
                }
                if ($this.DeliveryNotificationOption -contains 'Never') {
                    [MailKit.DeliveryStatusNotification]::Never
                }
            )
            if ($Output.Count -gt 0) {
                return $Output
            }
            return $null
        }
    }
    $SmtpClient = [MySmtpClient]::new() # [MailKit.Net.Smtp.SmtpClient]::new()

    if ($DeliveryNotificationOption) {
        # This requires custom class MySmtpClient
        $SmtpClient.DeliveryNotificationOption = $DeliveryNotificationOption
    }
    if ($DeliveryStatusNotificationType) {
        $SmtpClient.DeliveryStatusNotificationType = $DeliveryStatusNotificationType
    }

    if ($UseSsl) {
        $SmtpClient.Connect($Server, $Port, [MailKit.Security.SecureSocketOptions]::StartTls)
    } else {
        $SmtpClient.Connect($Server, $Port, $SecureSocketOptions)
    }
    if ($Credential) {
        [System.Text.Encoding] $SmtpEncoding = [System.Text.Encoding]::$Encoding
        $SmtpClient.Authenticate($SmtpEncoding, $SmtpCredentials, [System.Threading.CancellationToken]::None)
    }
    try {
        $SmtpClient.Send($Message)
        if (-not $Suppress) {
            [PSCustomObject] @{
                Status = $True
                Error  = ''
                SentTo = $MailSentTo
            }
        }
    } catch {
        if ($ShowErrors) {
            Write-Error $_
        } else {
            Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message)"
        }
        if (-not $Suppress) {
            [PSCustomObject] @{
                Status = $False
                Error  = $($_.Exception.Message)
                SentTo = $MailSentTo
            }
        }
    }
    $SmtpClient.Disconnect($true)
}
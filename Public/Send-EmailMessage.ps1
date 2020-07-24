function Send-EmailMessage {
    [cmdletBinding(DefaultParameterSetName = 'Compatibility', SupportsShouldProcess)]
    param(

        [alias('SmtpServer')][string] $Server,

        [int] $Port = 587,

        [object] $From,

        [string] $ReplyTo,

        [string[]] $Cc,

        [string[]] $Bcc,

        [string[]] $To,

        [string] $Subject,
        [ValidateSet('Low', 'Normal', 'High')][string] $Priority,

        [ValidateSet('ASCII', 'BigEndianUnicode', 'Default', 'Unicode', 'UTF32', 'UTF7', 'UTF8')][string] $Encoding = 'Default',

        [ValidateSet('None', 'OnSuccess', 'OnFailure', 'Delay', 'Never')][string[]] $DeliveryNotificationOption,

        [MailKit.Net.Smtp.DeliveryStatusNotificationType] $DeliveryStatusNotificationType,

        [pscredential] $Credential,

        [string] $Username,

        [string] $Password,

        [MailKit.Security.SecureSocketOptions] $SecureSocketOptions = [MailKit.Security.SecureSocketOptions]::Auto,
        [switch] $UseSsl,

        [alias('Body')][string[]] $HTML,

        [string[]] $Text,

        [alias('Attachments')][string[]] $Attachment,

        [int] $Timeout = 12000,

        [switch] $ShowErrors,
        [switch] $Suppress,

        [alias('EmailParameters')][System.Collections.IDictionary] $Email
    )
    if ($Email) {
        # Following code makes sure both formats are accepted.
        if ($Email.EmailTo) {
            $EmailParameters = $Email.Clone()
        } else {
            $EmailParameters = @{
                EmailFrom                   = $Email.From
                EmailTo                     = $Email.To
                EmailCC                     = $Email.CC
                EmailBCC                    = $Email.BCC
                EmailReplyTo                = $Email.ReplyTo
                EmailServer                 = $Email.Server
                EmailServerPassword         = $Email.Password
                EmailServerPasswordAsSecure = $Email.PasswordAsSecure
                EmailServerPasswordFromFile = $Email.PasswordFromFile
                EmailServerPort             = $Email.Port
                EmailServerLogin            = $Email.Login
                EmailServerEnableSSL        = $Email.EnableSsl
                EmailEncoding               = $Email.Encoding
                EmailEncodingSubject        = $Email.EncodingSubject
                EmailEncodingBody           = $Email.EncodingBody
                EmailSubject                = $Email.Subject
                EmailPriority               = $Email.Priority
                EmailDeliveryNotifications  = $Email.DeliveryNotifications
                EmailUseDefaultCredentials  = $Email.UseDefaultCredentials
            }
        }
        $From = $EmailParameters.EmailFrom
        $To = $EmailParameters.EmailTo
        $Cc = $EmailParameters.EmailCC
        $Bcc = $EmailParameters.EmailBCC
        $ReplyTo = $EmailParameters.EmailReplyTo
        $Server = $EmailParameters.EmailServer
        $Password = $EmailParameters.EmailServerPassword
        # $EmailServerPasswordAsSecure = $EmailParameters.EmailServerPasswordAsSecure
        # $EmailServerPasswordFromFile = $EmailParameters.EmailServerPasswordFromFile
        $Port = $EmailParameters.EmailServerPort
        $Username = $EmailParameters.EmailServerLogin
        #$UseSsl = $EmailParameters.EmailServerEnableSSL
        $Encoding = $EmailParameters.EmailEncoding
        #$EncodingSubject = $EmailParameters.EmailEncodingSubject
        $Encoding = $EmailParameters.EmailEncodingBody
        $Subject = $EmailParameters.EmailSubject
        $Priority = $EmailParameters.EmailPriority
        $DeliveryNotificationOption = $EmailParameters.EmailDeliveryNotifications
        #$EmailUseDefaultCredentials = $EmailParameters.EmailUseDefaultCredentials

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
    if ($Username -and $Password) {
        #void Authenticate(string userName, string password, System.Threading.CancellationToken cancellationToken)
    }

    $SmtpClient.Timeout = $Timeout

    try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", "Send-EmailMessage")) {
            $SmtpClient.Send($Message)
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status = $True
                    Error  = ''
                    SentTo = $MailSentTo
                }
            }
        }
    } catch {
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
            return
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
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

    .PARAMETER Cc
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

    .PARAMETER DeliveryNotificationOption
    Parameter description

    .PARAMETER DeliveryStatusNotificationType
    Parameter description

    .PARAMETER Credential
    Parameter description

    .PARAMETER Username
    Parameter description

    .PARAMETER Password
    Parameter description

    .PARAMETER SecureSocketOptions
    Parameter description

    .PARAMETER UseSsl
    Parameter description

    .PARAMETER HTML
    Parameter description

    .PARAMETER Text
    Parameter description

    .PARAMETER Attachment
    Parameter description

    .PARAMETER Timeout
    Parameter description

    .PARAMETER oAuth2
    Parameter description

    .PARAMETER Graph
    Parameter description

    .PARAMETER Email
    Parameter description

    .PARAMETER ShowErrors
    Parameter description

    .PARAMETER Suppress
    Parameter description

    .EXAMPLE
    An example

    .NOTES
    General notes
    #>
    [cmdletBinding(DefaultParameterSetName = 'Compatibility', SupportsShouldProcess)]
    param(
        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [alias('SmtpServer')][string] $Server,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [int] $Port = 587,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [object] $From,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [string] $ReplyTo,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [string[]] $Cc,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [string[]] $Bcc,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [string[]] $To,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [string] $Subject,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [alias('Importance')][ValidateSet('Low', 'Normal', 'High')][string] $Priority,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [ValidateSet('ASCII', 'BigEndianUnicode', 'Default', 'Unicode', 'UTF32', 'UTF7', 'UTF8')][string] $Encoding = 'Default',

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [ValidateSet('None', 'OnSuccess', 'OnFailure', 'Delay', 'Never')][string[]] $DeliveryNotificationOption,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [MailKit.Net.Smtp.DeliveryStatusNotificationType] $DeliveryStatusNotificationType,

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [pscredential] $Credential,

        [Parameter(ParameterSetName = 'ClearText')]
        [string] $Username,

        [Parameter(ParameterSetName = 'ClearText')]
        [string] $Password,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [MailKit.Security.SecureSocketOptions] $SecureSocketOptions = [MailKit.Security.SecureSocketOptions]::Auto,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [switch] $UseSsl,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [alias('Body')][string[]] $HTML,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [string[]] $Text,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [alias('Attachments')][string[]] $Attachment,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [int] $Timeout = 12000,

        [Parameter(ParameterSetName = 'oAuth')]
        [alias('oAuth')][switch] $oAuth2,

        [Parameter(ParameterSetName = 'Graph')]
        [switch] $Graph,

        [Parameter(ParameterSetName = 'Graph')]
        [switch] $DoNotSaveToSentItems,

        # Different feature set
        [Parameter(ParameterSetName = 'Grouped')]
        [alias('EmailParameters')][System.Collections.IDictionary] $Email,

        # Belongs to all parameter sets
        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Grouped')]
        [switch] $ShowErrors,

        [Parameter(ParameterSetName = 'ClearText')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Grouped')]
        [switch] $Suppress
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
            Write-Warning 'Send-EmailMessage - At least one To, CC or BCC is required.'
            return
        }
    }

    # lets define credentials early on, because if it's Graph we use different way to send emails
    if ($Credential) {
        if ($oAuth2.IsPresent) {
            $Authorization = ConvertFrom-OAuth2Credential -Credential $Credential
            $SaslMechanismOAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($Authorization.UserName, $Authorization.Token)
        } elseif ($Graph.IsPresent) {
            $sendGraphMailMessageSplat = @{
                From                 = $From
                To                   = $To
                Cc                   = $CC
                Bcc                  = $Bcc
                Subject              = $Subject
                HTML                 = $HTML
                Text                 = $Text
                Attachment           = $Attachment
                Credential           = $Credential
                Priority             = $Priority
                ReplyTo              = $ReplyTo
                DoNotSaveToSentItems = $DoNotSaveToSentItems
            }
            Remove-EmptyValue -Hashtable $sendGraphMailMessageSplat
            return Send-GraphMailMessage @sendGraphMailMessageSplat
        } else {
            $SmtpCredentials = $Credential
        }
    } elseif ($Username -and $Password) {
        #void Authenticate(string userName, string password, System.Threading.CancellationToken cancellationToken)
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

    [System.Text.Encoding] $SmtpEncoding = [System.Text.Encoding]::$Encoding

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
    $SmtpClient = [MySmtpClient]::new()
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
        if ($oAuth2.IsPresent) {
            $SmtpClient.Authenticate($SaslMechanismOAuth2)
        } elseif ($Graph.IsPresent) {
            # This is not going to happen is graph is used
        } else {
            $SmtpClient.Authenticate($SmtpEncoding, $SmtpCredentials, [System.Threading.CancellationToken]::None)
        }
    } elseif ($UserName -and $Password) {
        $SmtpClient.Authenticate($UserName, $Password, [System.Threading.CancellationToken]::None)
    }
    $SmtpClient.Timeout = $Timeout

    try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
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
function Send-EmailMessage {
    <#
    .SYNOPSIS
    The Send-EmailMessage cmdlet sends an email message from within PowerShell.

    .DESCRIPTION
    The Send-EmailMessage cmdlet sends an email message from within PowerShell. It replaces Send-MailMessage by Microsoft which is deprecated.

    .PARAMETER Server
    Specifies the name of the SMTP server that sends the email message.

    .PARAMETER Port
    Specifies an alternate port on the SMTP server. The default value is 587.

    .PARAMETER From
    This parameter specifies the sender's email address.

    .PARAMETER ReplyTo
    This property indicates the reply address. If you don't set this property, the Reply address is same as From address.

    .PARAMETER Cc
    Specifies the email addresses to which a carbon copy (CC) of the email message is sent.

    .PARAMETER Bcc
    Specifies the email addresses that receive a copy of the mail but are not listed as recipients of the message.

    .PARAMETER To
    Specifies the recipient's email address. If there are multiple recipients, separate their addresses with a comma (,)

    .PARAMETER Subject
    The Subject parameter isn't required. This parameter specifies the subject of the email message.

    .PARAMETER Priority
    Specifies the priority of the email message. Normal is the default. The acceptable values for this parameter are Normal, High, and Low.

    .PARAMETER Encoding
    Specifies the type of encoding for the target file. It's recommended to not change it.

    The acceptable values for this parameter are as follows:

    default:
    ascii: Uses the encoding for the ASCII (7-bit) character set.
    bigendianunicode: Encodes in UTF-16 format using the big-endian byte order.
    oem: Uses the default encoding for MS-DOS and console programs.
    unicode: Encodes in UTF-16 format using the little-endian byte order.
    utf7: Encodes in UTF-7 format.
    utf8: Encodes in UTF-8 format.
    utf32: Encodes in UTF-32 format.

    .PARAMETER DeliveryNotificationOption
    Specifies the delivery notification options for the email message. You can specify multiple values. None is the default value. The alias for this parameter is DNO. The delivery notifications are sent to the address in the From parameter. Multiple options can be chosen.

    .PARAMETER DeliveryStatusNotificationType
    Specifies delivery status notification type. Options are Full, HeadersOnly, Unspecified

    .PARAMETER Credential
    Specifies a user account that has permission to perform this action. The default is the current user.
    Type a user name, such as User01 or Domain01\User01. Or, enter a PSCredential object, such as one from the Get-Credential cmdlet.
    Credentials are stored in a PSCredential object and the password is stored as a SecureString.

    Credential parameter is also use to securely pass tokens/api keys for Graph API/oAuth2/SendGrid

    .PARAMETER Username
    Specifies UserName to use to login to server

    .PARAMETER Password
    Specifies Password to use to login to server. This is ClearText option and should not be used, unless used with SecureString

    .PARAMETER SecureSocketOptions
    Specifies secure socket option: None, Auto, StartTls, StartTlsWhenAvailable, SslOnConnect. Default is Auto.

    .PARAMETER UseSsl
    Specifies using StartTLS option. It's recommended to leave it disabled and use SecureSocketOptions which should take care of all security needs

    .PARAMETER SkipCertificateRevocation
    Specifies to skip certificate revocation check

    .PARAMETER SkipCertificateValidatation
    Specifies to skip certficate validation. Useful when using IP Address or self-generated certificates.

    .PARAMETER HTML
    HTML content to send email

    .PARAMETER Text
    Text content to send email. With SMTP one can define both HTML and Text. For SendGrid and Office 365 Graph API only HTML or Text will be used with HTML having priority

    .PARAMETER Attachment
    Specifies the path and file names of files to be attached to the email message.

    .PARAMETER Timeout
    Maximum time to wait to send an email via SMTP

    .PARAMETER oAuth2
    Send email via oAuth2

    .PARAMETER Graph
    Send email via Office 365 Graph API

    .PARAMETER SendGrid
    Send email via SendGrid API

    .PARAMETER SeparateTo
    Option separates each To field into separate emails (sent as one query). Supported by SendGrid only! BCC/CC are skipped when this mode is used.

    .PARAMETER DoNotSaveToSentItems
    Do not save email to SentItems when sending with Office 365 Graph API

    .PARAMETER Email
    Compatibility parameter for Send-Email cmdlet from PSSharedGoods

    .PARAMETER Suppress
    Do not display summary in [PSCustomObject]

    .PARAMETER AsSecureString
    Informs command that password provided is secure string, rather than clear text

    .EXAMPLE
    if (-not $MailCredentials) {
        $MailCredentials = Get-Credential
    }

    Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test.pl' } -To 'przemyslaw.klys@test.pl' `
        -Server 'smtp.office365.com' -SecureSocketOptions Auto -Credential $MailCredentials -HTML $Body -DeliveryNotificationOption OnSuccess -Priority High `
        -Subject 'This is another test email'

    .EXAMPLE
    if (-not $MailCredentials) {
        $MailCredentials = Get-Credential
    }
    # this is simple replacement (drag & drop to Send-MailMessage)
    Send-EmailMessage -To 'przemyslaw.klys@test.pl' -Subject 'Test' -Body 'test me' -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@test.pl' `
        -Attachments "$PSScriptRoot\..\README.MD" -Cc 'przemyslaw.klys@test.pl' -Priority High -Credential $MailCredentials `
        -UseSsl -Port 587 -Verbose

    .EXAMPLE
    # Use SendGrid Api
    $Credential = ConvertTo-SendGridCredential -ApiKey 'YourKey'

    Send-EmailMessage -From 'przemyslaw.klys@evo.cool' `
        -To 'przemyslaw.klys@evotec.pl', 'evotectest@gmail.com' `
        -Body 'test me Przemysław Kłys' `
        -Priority High `
        -Subject 'This is another test email' `
        -SendGrid `
        -Credential $Credential `
        -Verbose

    .EXAMPLE
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
    Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test1.pl' } -To 'przemyslaw.klys@test.pl' `
        -Credential $Credential -HTML $Body -Subject 'This is another test email 1' -Graph -Verbose -Priority High

    .EXAMPLE
    # Using OAuth2 for Office 365
    $ClientID = '4c1197dd-53'
    $TenantID = 'ceb371f6-87'

    $CredentialOAuth2 = Connect-oAuthO365 -ClientID $ClientID -TenantID $TenantID

    Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'test@evotec.pl' } -To 'test@evotec.pl' `
        -Server 'smtp.office365.com' -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High `
        -Subject 'This is another test email' -SecureSocketOptions Auto -Credential $CredentialOAuth2 -oAuth2

    .NOTES
    General notes
    #>
    [cmdletBinding(DefaultParameterSetName = 'Compatibility', SupportsShouldProcess)]
    param(
        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [alias('SmtpServer')][string] $Server,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [int] $Port = 587,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [object] $From,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [string] $ReplyTo,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [string[]] $Cc,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [string[]] $Bcc,

        [Parameter(ParameterSetName = 'SecureString')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [string[]] $To,

        [Parameter(ParameterSetName = 'SecureString')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [string] $Subject,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [alias('Importance')][ValidateSet('Low', 'Normal', 'High')][string] $Priority,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [ValidateSet('ASCII', 'BigEndianUnicode', 'Default', 'Unicode', 'UTF32', 'UTF7', 'UTF8')][string] $Encoding = 'Default',

        [Parameter(ParameterSetName = 'SecureString')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [ValidateSet('None', 'OnSuccess', 'OnFailure', 'Delay', 'Never')][string[]] $DeliveryNotificationOption,

        [Parameter(ParameterSetName = 'SecureString')]
        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [MailKit.Net.Smtp.DeliveryStatusNotificationType] $DeliveryStatusNotificationType,

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph', Mandatory)]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid', Mandatory)]
        [pscredential] $Credential,

        [Parameter(ParameterSetName = 'SecureString')]
        [string] $Username,

        [Parameter(ParameterSetName = 'SecureString')]

        [string] $Password,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [MailKit.Security.SecureSocketOptions] $SecureSocketOptions = [MailKit.Security.SecureSocketOptions]::Auto,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [switch] $UseSsl,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [switch] $SkipCertificateRevocation,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [switch] $SkipCertificateValidatation,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [alias('Body')][string[]] $HTML,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [string[]] $Text,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'SendGrid')]
        [alias('Attachments')][string[]] $Attachment,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [int] $Timeout = 12000,

        [Parameter(ParameterSetName = 'oAuth')]
        [alias('oAuth')][switch] $oAuth2,

        [Parameter(ParameterSetName = 'Graph')]
        [switch] $Graph,

        [Parameter(ParameterSetName = 'SecureString')]
        [switch] $AsSecureString,

        [Parameter(ParameterSetName = 'SendGrid')]
        [switch] $SendGrid,

        [Parameter(ParameterSetName = 'SendGrid')]
        [switch] $SeparateTo,

        [Parameter(ParameterSetName = 'Graph')]
        [switch] $DoNotSaveToSentItems,

        # Different feature set
        [Parameter(ParameterSetName = 'Grouped')]
        [alias('EmailParameters')][System.Collections.IDictionary] $Email,

        [Parameter(ParameterSetName = 'SecureString')]

        [Parameter(ParameterSetName = 'oAuth')]
        [Parameter(ParameterSetName = 'Compatibility')]
        [Parameter(ParameterSetName = 'Graph')]
        [Parameter(ParameterSetName = 'Grouped')]
        [Parameter(ParameterSetName = 'SendGrid')]
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
            if ($PSBoundParameters.ErrorAction -eq 'Stop') {
                Write-Error 'At least one To, CC or BCC is required.'
                return
            } else {
                Write-Warning 'Send-EmailMessage - At least one To, CC or BCC is required.'
                return
            }
        }
    }

    # lets define credentials early on, because if it's Graph we use different way to send emails
    if ($Credential) {
        if ($oAuth2.IsPresent) {
            $Authorization = ConvertFrom-OAuth2Credential -Credential $Credential
            $SaslMechanismOAuth2 = [MailKit.Security.SaslMechanismOAuth2]::new($Authorization.UserName, $Authorization.Token)
            $SmtpCredentials = $Credential
        } elseif ($Graph.IsPresent) {
            # Sending email via Office 365 Graph
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
                DoNotSaveToSentItems = $DoNotSaveToSentItems.IsPresent
            }
            Remove-EmptyValue -Hashtable $sendGraphMailMessageSplat
            return Send-GraphMailMessage @sendGraphMailMessageSplat
        } elseif ($SendGrid.IsPresent) {
            # Sending email via SendGrid
            $sendGraphMailMessageSplat = @{
                From       = $From
                To         = $To
                Cc         = $CC
                Bcc        = $Bcc
                Subject    = $Subject
                HTML       = $HTML
                Text       = $Text
                Attachment = $Attachment
                Credential = $Credential
                Priority   = $Priority
                ReplyTo    = $ReplyTo
                SeparateTo = $SeparateTo.IsPresent
            }
            Remove-EmptyValue -Hashtable $sendGraphMailMessageSplat
            return Send-SendGridMailMessage @sendGraphMailMessageSplat
        } else {
            $SmtpCredentials = $Credential
        }
    } elseif ($Username -and $Password -and $AsSecureString) {
        # Convert to SecureString
        try {
            $secStringPassword = ConvertTo-SecureString -ErrorAction Stop -String $Password
            $SmtpCredentials = [System.Management.Automation.PSCredential]::new($UserName, $secStringPassword)
        } catch {
            Write-Warning "Send-EmailMessage - Couldn't translate secure string to password. Error $($_.Exception.Message)"
            return
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
    if ($SkipCertificateRevocation) {
        $SmtpClient.CheckCertificateRevocation = $false
    }
    if ($SkipCertificateValidatation) {
        $CertificateCallback = [Net.ServicePointManager]::ServerCertificateValidationCallback
        [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        #$SmtpClient.ServerCertificateValidationCallback = [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    }
    if ($DeliveryNotificationOption) {
        # This requires custom class MySmtpClient
        $SmtpClient.DeliveryNotificationOption = $DeliveryNotificationOption
    }
    if ($DeliveryStatusNotificationType) {
        $SmtpClient.DeliveryStatusNotificationType = $DeliveryStatusNotificationType
    }
    if ($UseSsl) {
        # By default Auto is used, but if someone wants UseSSL that's fine too
        $SecureSocketOptions = [MailKit.Security.SecureSocketOptions]::StartTls
    }
    try {
        $SmtpClient.Connect($Server, $Port, $SecureSocketOptions)
        # Assign back certificate callback
        [Net.ServicePointManager]::ServerCertificateValidationCallback = $CertificateCallback
    } catch {
        # Assign back certificate callback
        [Net.ServicePointManager]::ServerCertificateValidationCallback = $CertificateCallback

        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
            return
        } else {
            Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message)"
            Write-Warning "Send-EmailMessage - Possible issue: Port? ($Port was used), Using SSL? ($SecureSocketOptions was used). You can also try SkipCertificateValidation or SkipCertificateRevocation. "
            if (-not $Suppress) {
                return [PSCustomObject] @{
                    Status = $False
                    Error  = $($_.Exception.Message)
                    SentTo = $MailSentTo
                }
            }
        }
    }
    if ($SmtpCredentials) {
        if ($oAuth2.IsPresent) {
            try {
                $SmtpClient.Authenticate($SaslMechanismOAuth2)
            } catch {
                if ($PSBoundParameters.ErrorAction -eq 'Stop') {
                    Write-Error $_
                    return
                } else {
                    Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message)"
                    if (-not $Suppress) {
                        return [PSCustomObject] @{
                            Status = $False
                            Error  = $($_.Exception.Message)
                            SentTo = $MailSentTo
                        }
                    }
                }
            }
        } elseif ($Graph.IsPresent) {
            # This is not going to happen is graph is used
        } else {
            try {
                $SmtpClient.Authenticate($SmtpEncoding, $SmtpCredentials, [System.Threading.CancellationToken]::None)
            } catch {
                if ($PSBoundParameters.ErrorAction -eq 'Stop') {
                    Write-Error $_
                    return
                } else {
                    Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message)"
                    if (-not $Suppress) {
                        return [PSCustomObject] @{
                            Status = $False
                            Error  = $($_.Exception.Message)
                            SentTo = $MailSentTo
                        }
                    }
                }
            }
        }
    } elseif ($UserName -and $Password) {
        try {
            $SmtpClient.Authenticate($UserName, $Password, [System.Threading.CancellationToken]::None)
        } catch {
            if ($PSBoundParameters.ErrorAction -eq 'Stop') {
                Write-Error $_
                return
            } else {
                Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message)"
                if (-not $Suppress) {
                    return [PSCustomObject] @{
                        Status = $False
                        Error  = $($_.Exception.Message)
                        SentTo = $MailSentTo
                    }
                }
            }
        }
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
        } else {
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status = $false
                    Error  = 'Email not sent (WhatIf)'
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
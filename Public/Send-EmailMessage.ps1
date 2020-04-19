function Send-EmailMessage {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][alias('SmtpServer')][string] $Server,
        [string] $Port = 587,
        [Parameter(Mandatory)][object] $From,
        [string] $ReplyTo,
        [Array] $Bcc,
        [Array] $To,
        [string] $Subject,
        [string] $Priority,
        [System.Text.Encoding] $Encoding = [System.Text.Encoding]::UTF8,
        [string] $DeliveryNotificationOption,
        [pscredential] $Credentials,
        [MailKit.Security.SecureSocketOptions] $SecureSocketOptions = [MailKit.Security.SecureSocketOptions]::Auto,
        [switch] $UseSsl,
        [string[]] $HTML,
        [string[]] $Text,
        [string[]] $Attachments,
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

    if ($Credentials) {
        $SmtpCredentials = $Credentials
    }

    $Message = [MimeKit.MimeMessage]::new()

    [MimeKit.InternetAddress] $SmtpFrom = ConvertTo-MailboxAddress -MailboxAddress $From
    $Message.From.Add($SmtpFrom)

    if ($To) {
        [MimeKit.InternetAddress[]] $SmtpTo = ConvertTo-MailboxAddress -MailboxAddress $To
        $Message.To.AddRange($SmtpTo)
    }
    if ($Cc) {
        [MimeKit.InternetAddress[]] $SmtpCC = ConvertTo-MailboxAddress -MailboxAddress $Cc
        $Message.To.Add($SmtpCC)
    }
    if ($Bcc) {
        [MimeKit.InternetAddress[]] $SmtpBcc = ConvertTo-MailboxAddress -MailboxAddress $Bcc
        $Message.To.Add($SmtpBcc)
    }

    if ($ReplyTo) {
        [MimeKit.InternetAddress] $SmtpReplyTo = ConvertTo-MailboxAddress -MailboxAddress $ReplyTo
        $Message.ReplyTo.Add($SmtpReplyTo)
    }

    if ($Subject) {
        $Message.Subject = $Subject
    }
    if ($Body) {
        <#
        MimeKit.TextPart new(MimeKit.MimeEntityConstructorArgs args)
        MimeKit.TextPart new(string subtype, Params System.Object[] args)
        MimeKit.TextPart new(string subtype)
        MimeKit.TextPart new(MimeKit.Text.TextFormat format)
        MimeKit.TextPart new()

        var message = new MimeMessage();
        message.Body = new TextPart ("html") { Text = "<b>Test Message</b>" };

        #>
        #$BodyText = [MimeKit.TextPart]::new()
        #$BodyText.Text = $Body

        $BodyBuilder = [MimeKit.BodyBuilder]::new()
        if ($BodyAsHtml) {
            $BodyBuilder.HtmlBody = $Body
        } else {
            $BodyBuilder.TextBody = $Body
        }
        $Message.Body = $BodyBuilder.ToMessageBody()
    }


    $SmtpClient = [MailKit.Net.Smtp.SmtpClient]::new()
    <#
    void Connect(string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void Connect(System.Net.Sockets.Socket socket, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void Connect(System.IO.Stream stream, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void Connect(uri uri, System.Threading.CancellationToken cancellationToken)
    void Connect(string host, int port, bool useSsl, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(string host, int port, bool useSsl, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(System.Net.Sockets.Socket socket, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    void IMailService.Connect(System.IO.Stream stream, string host, int port, MailKit.Security.SecureSocketOptions options, System.Threading.CancellationToken cancellationToken)
    #>
    if ($UseSsl) {
        $SmtpClient.Connect($Server, $Port, $UseSsl.IsPresent)
    } else {
        $SmtpClient.Connect($Server, $Port, $SecureSocketOptions)
    }
    if ($Credentials) {
        $SmtpClient.Authenticate($Encoding, $SmtpCredentials, [System.Threading.CancellationToken]::None)
    }
    try {
        $SmtpClient.Send($Message)
        if (-not $Suppress) {
            @{
                Status = $True
                Error  = ""
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
            @{
                Status = $False
                Error  = $($_.Exception.Message)
                SentTo = ""
            }
        }
    }
    $SmtpClient.Disconnect($true)
}
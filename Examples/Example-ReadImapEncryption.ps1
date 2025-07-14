Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# Fetch messages and detect encryption
Get-IMAPMessage -Client $client -All |
    ForEach-Object {
        Write-Host "Message $($_.Uid.Id) encryption: $($_.Encryption)"
        $decrypted = $_
        if ($_.Encryption -eq [Mailozaurr.EmailEncryption]::PgpEncrypted) {
            $decrypted = $_ | Unprotect-MimeMessage -PrivateKeyPath 'C:\Keys\private.asc' -PrivateKeyPassword 'passphrase'
        } elseif ($_.Encryption -eq [Mailozaurr.EmailEncryption]::SmimeEncrypted) {
            $cert = Get-PfxCertificate -FilePath 'C:\Keys\email.pfx' -Password (ConvertTo-SecureString 'pfx-pass' -AsPlainText -Force)
            $decrypted = $_ | Unprotect-MimeMessage -Certificate $cert
        }
        $decrypted | Save-MimeMessage -Path "$Env:TEMP\$($_.Uid.Id).eml"
        $content = $decrypted | Get-MimeMessageContent
        $content.TextBody
        $content.HtmlBody
    }

Disconnect-IMAP -Client $client

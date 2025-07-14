Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# Fetch messages and detect encryption
Get-IMAPMessage -Client $client -All |
    ForEach-Object {
        Write-Host "Message $($_.Uid.Id) encryption: $($_.Encryption)"
        if ($_.Encryption -eq [Mailozaurr.EmailEncryption]::PgpEncrypted) {
            $_ | Unprotect-MimeMessage -PrivateKeyPath 'C:\Keys\private.asc' -PrivateKeyPassword 'passphrase' | Out-Null
        } elseif ($_.Encryption -eq [Mailozaurr.EmailEncryption]::SmimeEncrypted) {
            $cert = Get-PfxCertificate -FilePath 'C:\Keys\email.pfx' -Password (ConvertTo-SecureString 'pfx-pass' -AsPlainText -Force)
            $_ | Unprotect-MimeMessage -Certificate $cert | Out-Null
        }
    }

Disconnect-IMAP -Client $client

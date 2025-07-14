Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $cred -Port 995 -UseSsl

Get-POP3Message -Client $client -All |
    ForEach-Object {
        Write-Host "Message $($_.Index) encryption: $($_.Encryption)"
        if ($_.Encryption -eq [Mailozaurr.EmailEncryption]::PgpEncrypted) {
            $_ | Unprotect-MimeMessage -PrivateKeyPath 'C:\Keys\private.asc' -PrivateKeyPassword 'passphrase' | Out-Null
        } elseif ($_.Encryption -eq [Mailozaurr.EmailEncryption]::SmimeEncrypted) {
            $cert = Get-PfxCertificate -FilePath 'C:\Keys\email.pfx' -Password (ConvertTo-SecureString 'pfx-pass' -AsPlainText -Force)
            $_ | Unprotect-MimeMessage -Certificate $cert | Out-Null
        }
    }

Disconnect-POP3 -Client $client

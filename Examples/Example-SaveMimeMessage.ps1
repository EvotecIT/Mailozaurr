Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

$msg = Get-IMAPMessage -Client $client -SequenceStart 0 -SequenceEnd 0
$mime = $msg
if ($msg.Encryption.ToString() -eq 'PgpEncrypted') {
    $mime = $msg | Unprotect-MimeMessage -PrivateKeyPath 'C:\Keys\private.asc' -PrivateKeyPassword 'passphrase'
} elseif ($msg.Encryption.ToString() -eq 'SmimeEncrypted') {
    $cert = Get-PfxCertificate -FilePath 'C:\Keys\email.pfx' -Password (ConvertTo-SecureString 'pfx-pass' -AsPlainText -Force)
    $mime = $msg | Unprotect-MimeMessage -Certificate $cert
}

# Save to disk in EML format
$mime | Save-MimeMessage -Path "$Env:TEMP\latest.eml"

# Extract plain text and HTML bodies
$content = $mime | Get-MimeMessageContent
$content.TextBody
$content.HtmlBody

Disconnect-IMAP -Client $client

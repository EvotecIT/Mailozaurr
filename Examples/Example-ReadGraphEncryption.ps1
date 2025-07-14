Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-GraphCredential -ClientId 'clientId' -ClientSecret 'secret' -DirectoryId 'tenantId'
Connect-EmailGraph -Credential $cred | Out-Null

$msg = Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -First 1
$mime = $msg | Get-EmailGraphMessageMime
Write-Host "Message $($msg.Id) encryption: $($mime.Encryption)"
$decrypted = $mime
if ($mime.Encryption -eq [Mailozaurr.EmailEncryption]::PgpEncrypted) {
    $decrypted = $mime | Unprotect-MimeMessage -PrivateKeyPath 'C:\Keys\private.asc' -PrivateKeyPassword 'passphrase'
} elseif ($mime.Encryption -eq [Mailozaurr.EmailEncryption]::SmimeEncrypted) {
    $cert = Get-PfxCertificate -FilePath 'C:\Keys\email.pfx' -Password (ConvertTo-SecureString 'pfx-pass' -AsPlainText -Force)
    $decrypted = $mime | Unprotect-MimeMessage -Certificate $cert
}
$decrypted | Save-MimeMessage -Path "$Env:TEMP\graph.eml"
$content = $decrypted | Get-MimeMessageContent
$content.TextBody
$content.HtmlBody
Disconnect-EmailGraph

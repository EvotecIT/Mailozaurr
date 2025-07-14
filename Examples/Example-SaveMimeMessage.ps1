Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

$msg = Get-IMAPMessage -Client $client -SequenceStart 0 -SequenceEnd 0
$mime = $msg | Unprotect-MimeMessage

# Save to disk in EML format
$mime | Save-MimeMessage -Path "$Env:TEMP\latest.eml"

# Extract plain text and HTML bodies
$content = $mime | Get-MimeMessageContent
$content.TextBody
$content.HtmlBody

Disconnect-IMAP -Client $client

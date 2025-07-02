Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# Preview junk cleanup
Clear-IMAPJunk -Client $imap -Preview

Disconnect-IMAP -Client $imap

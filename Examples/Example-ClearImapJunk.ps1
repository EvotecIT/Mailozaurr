Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# Preview junk cleanup skipping messages from boss@example.com
Clear-IMAPJunk -Client $imap -Preview -SkipFrom 'boss@example.com'

Disconnect-IMAP -Client $imap

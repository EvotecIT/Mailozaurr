Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Search IMAP mailbox body
$imap = Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential)
$imapMsgs = Search-IMAPMailbox -Client $imap -BodyContains 'invoice'
$imapMsgs | ForEach-Object { Write-Host "IMAP found $($_.Message.Subject)" }
Disconnect-IMAP -Client $imap

# Search POP3 mailbox body
$pop = Connect-POP3 -Server 'pop.example.com' -Credential (Get-Credential)
$popMsgs = Search-POP3Mailbox -Client $pop -BodyContains 'invoice'
$popMsgs | ForEach-Object { Write-Host "POP3 found $($_.Message.Subject)" }
Disconnect-POP3 -Client $pop

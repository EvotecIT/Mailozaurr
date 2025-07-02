Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Search an IMAP mailbox
$imap = Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential)
$imapMsgs = Search-IMAPMailbox -Client $imap -Subject 'Invoice'
$imapMsgs | ForEach-Object { Write-Host "Found IMAP message $($_.Message.Subject)" }
# Limit results directly with -Count to stop searching after the first match
$firstImap = Search-IMAPMailbox -Client $imap -Subject 'Invoice' -Count 1
# Alternatively search all messages then pick the first
$firstImapAlt = ($imapMsgs | Select-Object -First 1)
Disconnect-IMAP -Client $imap

# Search a POP3 mailbox
$pop = Connect-POP3 -Server 'pop.example.com' -Credential (Get-Credential)
$popMsgs = Search-POP3Mailbox -Client $pop -FromContains 'contoso.com' -Count 1
$popMsgs | ForEach-Object { Write-Host "Found POP3 message $($_.Message.Subject)" }
Disconnect-POP3 -Client $pop

# Search a Graph mailbox for a message
$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
$graph = Connect-EmailGraph -Credential $cred
$gm = Search-GraphMailbox -Connection $graph -UserPrincipalName 'user@example.com' -Query 'subject:Report' -Count 1

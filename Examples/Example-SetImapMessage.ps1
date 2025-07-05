Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# 1. Show the first 10 messages with their UIDs
Get-IMAPMessage -Client $client -All | Select-Object -First 10 Uid, From, Subject

# 2. Display which email has UID 10
Get-IMAPMessage -Client $client -Uid 10 | ForEach-Object { $_.Raw.Message }

# 3. Mark message with UID 10 as read
Set-IMAPMessage -Client $client -Uid 10 -Read

# 4. Mark message UID 5 from 'Archive' as unread
Set-IMAPMessage -Client $client -Uid 5 -Unread -Folder 'Archive'

# 5. Find messages from a sender with a matching subject
$reports = Get-IMAPMessage -Client $client -FromContains 'alerts@example.com' -Subject 'Report'

# 6. Mark all matching messages as read
foreach ($msg in $reports) { Set-IMAPMessage -Client $client -Uid $msg.Uid.Id -Read }

# 7. Change folder to 'Archive'
Set-IMAPFolder -Client $client -Path 'Archive'

# 8. List the top 5 messages from the new folder
Get-IMAPMessage -Client $client -All | Select-Object -First 5 Uid, Subject

# 9. Mark every message in the folder as read
Get-IMAPMessage -Client $client -All | ForEach-Object { Set-IMAPMessage -Client $client -Uid $_.Uid.Id -Read }

# 10. Disconnect
Disconnect-IMAP -Client $client

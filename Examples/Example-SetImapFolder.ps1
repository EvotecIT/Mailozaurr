Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# 1. Display the current folder
$client.Folder.FullName

# 2. Change the working folder to 'Archive'
Set-IMAPFolder -Client $client -Path 'Archive'

# 3. List first 10 messages from 'Archive'
Get-IMAPMessage -Client $client -All | Select-Object -First 10 Uid, Subject

# 4. Switch to a nested folder with write access
Set-IMAPFolder -Client $client -Path 'Projects/2025' -FolderAccess ReadWrite

# 5. Show message count in the new folder
$client.Count

# 6. Return to Inbox
Set-IMAPFolder -Client $client -Path 'INBOX'

# 7. Switch to Sent Items
Set-IMAPFolder -Client $client -Path '[Gmail]/Sent Mail'

# 8. Mark a message in Sent Items as read
Set-IMAPMessage -Client $client -Uid 123 -Read

# 9. Change back to 'Archive'
Set-IMAPFolder -Client $client -Path 'Archive'

# 10. Disconnect
Disconnect-IMAP -Client $client

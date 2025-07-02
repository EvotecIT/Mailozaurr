Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# 1. Find messages with a specific subject
Get-IMAPMessage -Client $client -Subject 'Invoice'

# 2. Messages from a sender in the last 7 days
Get-IMAPMessage -Client $client -FromContains 'alerts@example.com' -Since (Get-Date).AddDays(-7)

# 3. Messages sent to a department mailbox
Get-IMAPMessage -Client $client -ToContains 'sales@example.com'

# 4. High priority messages only
Get-IMAPMessage -Client $client -Priority High

# 5. Messages that have attachments
Get-IMAPMessage -Client $client -HasAttachment

# 6. Delete old reports older than 30 days
Get-IMAPMessage -Client $client -Subject 'Report' -Before (Get-Date).AddDays(-30) -Delete

# 7. Fetch messages by UID range
Get-IMAPMessage -Client $client -UidStart 1 -UidEnd 20

# 8. Fetch messages by sequence numbers
Get-IMAPMessage -Client $client -SequenceStart 1 -SequenceEnd 5

# 9. Messages from the last 24 hours that include attachments
Get-IMAPMessage -Client $client -Since (Get-Date).AddDays(-1) -HasAttachment

# 10. Delete high priority messages from a specific sender
Get-IMAPMessage -Client $client -FromContains 'alerts@example.com' -Priority High -Delete

Disconnect-IMAP -Client $client

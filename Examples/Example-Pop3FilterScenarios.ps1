Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $cred -Port 995 -Options Auto

# 1. Retrieve messages with subject 'Invoice'
Get-POP3Message -Client $client -Subject 'Invoice'

# 2. Messages from a sender in the past week
Get-POP3Message -Client $client -FromContains 'alerts@example.com' -Since (Get-Date).AddDays(-7)

# 3. Messages sent to the sales team
Get-POP3Message -Client $client -ToContains 'sales@example.com'

# 4. High priority messages
Get-POP3Message -Client $client -Priority High

# 5. Messages that include attachments
Get-POP3Message -Client $client -HasAttachment

# 6. Delete old reports older than 30 days
Get-POP3Message -Client $client -Subject 'Report' -Before (Get-Date).AddDays(-30) -Delete

# 7. Retrieve all messages and remove them
Get-POP3Message -Client $client -All -Delete

# 8. Fetch messages delivered after a date
Get-POP3Message -Client $client -Since (Get-Date).AddDays(-14)

# 9. Fetch messages delivered before a date
Get-POP3Message -Client $client -Before (Get-Date).AddDays(-60)

# 10. Delete messages from a specific sender with attachments
Get-POP3Message -Client $client -FromContains 'newsletter@example.com' -HasAttachment -Delete

Disconnect-POP3 -Client $client

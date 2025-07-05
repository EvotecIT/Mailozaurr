Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $cred -Port 995 -Options Auto

# 1. List first 10 messages with their indexes
Get-POP3Message -Client $client -All | Select-Object -First 10 Index, From, Subject

# 2. Display the message stored at index 1
Get-POP3Message -Client $client -Index 1 | ForEach-Object { $_.Raw.Message }

# 3. Mark the first message as read
Set-POP3Message -Client $client -Index 0 -Read

# 4. Mark the second message as unread
Set-POP3Message -Client $client -Index 1 -Unread

# 5. Find messages from a sender with a subject filter
$matches = Get-POP3Message -Client $client -FromContains 'alerts@example.com' -Subject 'Report'

# 6. Mark all matching messages as read
foreach ($msg in $matches) { Set-POP3Message -Client $client -Index $msg.Index -Read }

# 7. Mark every message as unread
for ($i = 0; $i -lt $client.Count; $i++) { Set-POP3Message -Client $client -Index $i -Unread }

# 8. List the top 5 messages again
Get-POP3Message -Client $client -All | Select-Object -First 5 Index, Subject

# 9. Fetch a specific message by index
$firstMsg = Get-POP3Message -Client $client -Index 0

# 10. Disconnect
Disconnect-POP3 -Client $client

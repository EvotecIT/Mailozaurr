Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

# 1. Show the first 10 messages with their IDs
Get-EmailGraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -Limit 10 |
    Select-Object Id, From, Subject

# 2. Display details for the newest message
$m = Get-EmailGraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -Limit 1
$m.Raw | Format-List

# 3. Mark the message as read
Set-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -Read -Connection $graph

# 4. Mark it as unread again
Set-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -Read:$false -Connection $graph

# 5. Find messages from a sender with a given subject
$reports = Get-EmailGraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -FromContains 'alerts@example.com' -Subject 'Report'

# 6. Mark all matching messages as read
foreach ($msg in $reports) { Set-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $msg.Id -Read -Connection $graph }

# 7. Move the processed messages to Archive
foreach ($msg in $reports) { Move-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $msg.Id -DestinationFolderId 'Archive' -Connection $graph }

# 8. Wait for a new message
$listener = Wait-GraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -TimeoutSeconds 60

# 9. Output the subject of the arrived message
$listener.Message | Select-Object -ExpandProperty subject

# 10. Disconnect
Disconnect-EmailGraph -Connection $graph

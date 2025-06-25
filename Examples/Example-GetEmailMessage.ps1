Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Example 1: Retrieve messages from an IMAP folder with subject filter
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -Folder 'Inbox/Reports' -Subject 'Monthly' -Since (Get-Date).AddDays(-7)
Disconnect-IMAP -Client $imap

# Example 2: Retrieve today's POP3 messages
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -Since (Get-Date).Date
Disconnect-POP3 -Client $pop

# Example 3: Retrieve all POP3 messages and delete them
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -All -Delete
Disconnect-POP3 -Client $pop

# Example 4: Retrieve high priority messages from a specific domain via IMAP
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -FromContains 'microsoft.com' -Priority High
Disconnect-IMAP -Client $imap

# Example 5: Retrieve IMAP messages from a folder with subject and sender filter
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -Folder 'Inbox/Alerts' -Subject 'Alert' -FromContains 'contoso.com'
Disconnect-IMAP -Client $imap

# Example 6: Retrieve POP3 messages matching a subject
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -Subject 'Invoice'
Disconnect-POP3 -Client $pop

# Example 7: Retrieve IMAP messages sent to a support address
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -ToContains 'support@example.com'
Disconnect-IMAP -Client $imap

# Example 8: Retrieve IMAP messages from the last month
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -Since (Get-Date).AddMonths(-1) -Before (Get-Date)
Disconnect-IMAP -Client $imap

# Example 9: Retrieve POP3 messages from microsoft.com last week and delete them
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -FromContains 'microsoft.com' -Since (Get-Date).AddDays(-7) -Delete
Disconnect-POP3 -Client $pop

# Example 10: Retrieve all IMAP messages ignoring filters
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -All
Disconnect-IMAP -Client $imap

# Example 11: Retrieve POP3 messages sent to the sales alias
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -ToContains 'sales@example.com'
Disconnect-POP3 -Client $pop

# Example 12: Retrieve POP3 messages marked low priority
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -Priority Low
Disconnect-POP3 -Client $pop

# Example 13: Retrieve and purge all messages from the Spam folder
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -Folder 'Spam' -All -Delete
Disconnect-IMAP -Client $imap

# Example 14: Retrieve POP3 messages received after a specific date
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -Since (Get-Date '2025-01-01')
Disconnect-POP3 -Client $pop

# Example 15: Retrieve IMAP messages from one sender with a subject filter
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -FromContains 'alerts@example.com' -Subject 'Server'
Disconnect-IMAP -Client $imap


# Example 16: Retrieve IMAP messages that have attachments
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -HasAttachment
Disconnect-IMAP -Client $imap

# Example 17: Retrieve IMAP messages from contoso.com with 'Report' in subject that have attachments
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -FromContains 'contoso.com' -Subject 'Report' -HasAttachment
Disconnect-IMAP -Client $imap

# Example 18: Retrieve Graph messages with attachments
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred
Get-EmailMessage -UserPrincipalName 'user@example.com' -Connection $graph -HasAttachment -Limit 5

Disconnect-EmailGraph -Connection $graph

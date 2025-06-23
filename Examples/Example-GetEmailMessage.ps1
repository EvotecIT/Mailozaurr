Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Retrieve messages from an IMAP folder with filters
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -Folder 'Inbox/Reports' -Subject 'Monthly' -Since (Get-Date).AddDays(-7)
Disconnect-IMAP -Client $imap

# Retrieve today's POP3 messages
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -Since (Get-Date).Date
Disconnect-POP3 -Client $pop

# Retrieve all POP3 messages and delete them
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 995 -Options Auto
Get-EmailMessage -PopClient $pop -All -Delete
Disconnect-POP3 -Client $pop

# Retrieve high priority messages from a specific domain via IMAP
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user@example.com' -Password 'Pa55w0rd' -Port 993 -Options Auto
Get-EmailMessage -ImapClient $imap -FromContains 'microsoft.com' -Priority High
Disconnect-IMAP -Client $imap

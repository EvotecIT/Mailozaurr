Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Authenticate however you like
$cred = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# list inbox messages
Get-IMAPFolder -Client $client
Get-IMAPMessage -Client $client | Select-Object -First 10

# open a nested folder
Get-IMAPFolder -Client $client -Path 'Inbox/Reports/2024'
Get-IMAPMessage -Client $client | Select-Object -First 10

# sent items
Get-IMAPFolder -Client $client -Path 'Sent'
Get-IMAPMessage -Client $client | Select-Object -First 10

# deleted items
Get-IMAPFolder -Client $client -Path 'Deleted Items'
Get-IMAPMessage -Client $client | Select-Object -First 10

Disconnect-IMAP -Client $client

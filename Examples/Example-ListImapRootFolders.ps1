Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Acquire credentials using your preferred method
$credential = Get-Credential

# Connect and list root folders
$client = Connect-IMAP -Server 'imap.example.com' -Credential $credential -Port 993 -Options Auto
Get-IMAPFolder -Client $client -Root

Disconnect-IMAP -Client $client

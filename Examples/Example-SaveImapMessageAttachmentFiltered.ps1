Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $credential -Port 993 -Options Auto

# Fetch recent messages with attachments matching a subject filter
Get-IMAPMessage -Client $client -Subject 'Report' -Since (Get-Date).AddDays(-7) -HasAttachment |
    ForEach-Object { Save-IMAPMessageAttachment -Client $client -Uid $_.Uid.Id -Path "$Env:UserProfile\Downloads\MailAttachments" }

Disconnect-IMAP -Client $client

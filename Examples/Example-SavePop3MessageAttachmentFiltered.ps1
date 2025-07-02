Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $credential -Port 995 -Options Auto

# Search for recent messages with attachments matching subject
Get-POP3Message -Client $client -Subject 'Report' -Since (Get-Date).AddDays(-7) -HasAttachment |
    ForEach-Object { Save-POP3MessageAttachment -Client $client -Index $_.Index -Path "$Env:UserProfile\Downloads\MailAttachments" }

Disconnect-POP3 -Client $client

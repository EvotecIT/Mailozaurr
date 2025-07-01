Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $credential -Port 995 -Options Auto

# Download attachments from the first message
Save-POP3MessageAttachment -Client $client -Index 0 -Path "$Env:UserProfile\Downloads\MailAttachments"

Disconnect-POP3 -Client $client

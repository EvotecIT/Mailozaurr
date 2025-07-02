Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $credential -Port 993 -Options Auto

# Download attachments from the newest message
Get-IMAPMessage -Client $client -SequenceStart 0 -SequenceEnd 0 -HasAttachment |
    ForEach-Object { Save-IMAPMessageAttachment -Client $client -Uid $_.Uid.Id -Path "$Env:UserProfile\Downloads\MailAttachments" }

Disconnect-IMAP -Client $client

Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credential = Get-Credential
$Client = Connect-IMAP -Server 'imap.gmail.com' -Credential $Credential -Port 993 -Options Auto
Get-IMAPFolder -Client $Client -Verbose

## Not yet sure how to best process messages
#Get-IMAPMessage -Client $Client -Verbose
#foreach ($folder in $client.Data.Inbox.GetSubfolders($false)) {
#    "[folder] {0}", $folder.Name
#}

Disconnect-IMAP -Client $Client
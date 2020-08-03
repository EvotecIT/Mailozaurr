Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientID = '9393330741'
$ClientSecret = 'gk2ztAGU'

$oAuth2 = Connect-oAuthGoogle -ClientID $ClientID -ClientSecret $ClientSecret -GmailAccount 'evotectest@gmail.com' -Scope https://mail.google.com/
$Client = Connect-IMAP -Server 'imap.gmail.com' -Port 993 -Options Auto -Credential $oAuth2 -oAuth2

Get-IMAPFolder -Client $Client -Verbose

## Not yet sure how to best process messages
#Get-IMAPMessage -Client $Client -Verbose
#foreach ($folder in $client.Data.Inbox.GetSubfolders($false)) {
#    "[folder] {0}", $folder.Name
#}

Disconnect-IMAP -Client $Client -Verbose
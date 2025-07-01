Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientID = '9393330741'
$ClientSecret = 'gk2ztAGU'

$oAuth2 = Connect-OAuthGoogle -ClientID $ClientID -ClientSecret $ClientSecret -GmailAccount 'evotectest@gmail.com' -Scope https://mail.google.com/
$Client = Connect-IMAP -Server 'imap.gmail.com' -Port 993 -Options Auto -Credential $oAuth2 -OAuth2

Get-IMAPFolder -Client $Client -Verbose
Get-IMAPMessage -Client $Client -All -Delete

Disconnect-IMAP -Client $Client -Verbose


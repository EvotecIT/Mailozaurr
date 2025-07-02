Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientID = '9393330741'
$ClientSecret = 'gk2ztAGU'

$oAuth2 = Connect-OAuthGoogle -ClientID $ClientID -ClientSecret $ClientSecret -GmailAccount 'evotectest@gmail.com' -Scope https://mail.google.com/
$Client = Connect-IMAP -Server 'imap.gmail.com' -Port 993 -Options Auto -Credential $oAuth2 -OAuth2

Get-IMAPFolder -Client $Client -Verbose
# Delete messages from a specific sender using OAuth authentication
Get-IMAPMessage -Client $Client -FromContains 'alerts@example.com' -Since (Get-Date).AddDays(-7) -Delete

Disconnect-IMAP -Client $Client -Verbose


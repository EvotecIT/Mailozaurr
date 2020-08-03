Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientID = '93933307418'
$ClientSecret = 'gk2ztAG'

$oAuth2 = Connect-oAuthGoogle -ClientID $ClientID -ClientSecret $ClientSecret -GmailAccount 'email@gmail.com' -Scope https://mail.google.com/
$Client = Connect-POP3 -Server 'pop.gmail.com' -Credential $oAuth2 -Port 995 -Options Auto -oAuth2
Get-POP3Message -Client $Client -Index 0 -Count 5 | Format-Table
Save-POP3Message -Client $Client -Index 7 -Path "$Env:UserProfile\Desktop\mail7.eml"
Disconnect-POP3 -Client $Client -Verbose
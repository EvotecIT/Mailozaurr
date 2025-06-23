Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$GmailAccount = 'user@gmail.com'
$ClientID = 'Your-Client-ID'
$ClientSecret = 'Your-Client-Secret'
$Scopes = @('https://mail.google.com/')

$Credential = Connect-OAuthGoogle -GmailAccount $GmailAccount -ClientID $ClientID -ClientSecret $ClientSecret -Scope $Scopes

$CredInfo = ConvertFrom-OAuth2Credential -Credential $Credential
$CredInfo | Format-List

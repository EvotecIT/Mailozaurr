Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Connect-OAuthGoogle launches a browser sign-in flow and returns a credential with the acquired token
$clientSecret = Read-Host 'Google client secret' -AsSecureString
$oauthCred = Connect-OAuthGoogle -GmailAccount 'user@gmail.com' -ClientID 'id' -ClientSecretSecureString $clientSecret -Scope https://mail.google.com/

# If you already obtained an access token elsewhere, ConvertTo-OAuth2Credential wraps it as a PSCredential
$accessToken = Read-Host 'OAuth access token' -AsSecureString
$tokenCred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -TokenSecureString $accessToken

$oauthCred
$tokenCred

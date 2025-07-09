Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Connect-OAuthGoogle launches a browser sign-in flow and returns a credential with the acquired token
$oauthCred = Connect-OAuthGoogle -GmailAccount 'user@gmail.com' -ClientID 'id' -ClientSecret 'secret' -Scope https://mail.google.com/

# If you already obtained an access token elsewhere, ConvertTo-OAuth2Credential wraps it as a PSCredential
$tokenCred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'ya29.a0Af...'

$oauthCred
$tokenCred

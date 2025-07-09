Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Acquire OAuth2 credential using Connect-OAuthGoogle
# $cred = Connect-OAuthGoogle -GmailAccount 'user@gmail.com' -ClientID 'id' -ClientSecret 'secret'

# Or create a credential from an existing token
$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

Send-EmailMessage -EmailProvider Gmail -GmailAccount 'user@gmail.com' -From 'user@gmail.com' -To 'recipient@example.com' -Credential $cred -Subject 'Gmail API Test' -Body 'Hello from Gmail API' -WhatIf

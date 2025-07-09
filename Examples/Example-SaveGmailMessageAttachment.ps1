Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Acquire OAuth2 credential using Connect-OAuthGoogle
# $cred = Connect-OAuthGoogle -GmailAccount 'user@gmail.com' -ClientID 'id' -ClientSecret 'secret'

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

Save-GmailMessageAttachment -GmailAccount 'user@gmail.com' -Credential $cred -Id 'abcd1234' -Path $Env:TEMP -WhatIf

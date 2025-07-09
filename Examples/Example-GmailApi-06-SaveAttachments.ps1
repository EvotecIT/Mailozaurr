Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

Save-GmailMessageAttachment -GmailAccount 'user@gmail.com' -Credential $cred -Id 'abcd1234' -Path $Env:TEMP

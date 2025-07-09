Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

Remove-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred -Id 'abcd1234' -Confirm:$false

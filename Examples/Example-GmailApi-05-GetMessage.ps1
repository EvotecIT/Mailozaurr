Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

# Retrieve a single message by id and save it
$msg = Get-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred -Id 'abcd1234'
$msg.Raw | Out-File "$Env:TEMP/message.eml"

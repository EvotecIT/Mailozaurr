Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

Send-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred `
    -From 'user@gmail.com' -To 'recipient@example.com' `
    -Subject 'Test API message' -TextBody 'Hello from Gmail API'

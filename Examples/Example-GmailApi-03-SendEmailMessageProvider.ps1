Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

Send-EmailMessage -EmailProvider Gmail -GmailAccount 'user@gmail.com' `
    -From 'user@gmail.com' -To 'recipient@example.com' -Credential $cred \
    -Subject 'Provider example' -Body 'Sent via Send-EmailMessage'

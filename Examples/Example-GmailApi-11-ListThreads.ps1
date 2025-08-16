Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

# List last 5 threads with alerts
Get-GmailThread -GmailAccount 'user@gmail.com' -Credential $cred `
    -Query 'from:alerts@example.com' -MaxResults 5

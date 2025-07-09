Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

$messages = Get-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred `
    -Query 'subject:"Report" older_than:7d'
foreach ($m in $messages) {
    Remove-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred -Id $m.Id
}

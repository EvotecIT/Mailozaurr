Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

$messages = Get-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred `
    -Query 'has:attachment newer_than:1d'
foreach ($m in $messages) {
    Save-GmailMessageAttachment -GmailAccount 'user@gmail.com' -Credential $cred -Id $m.Id -Path $Env:TEMP
}

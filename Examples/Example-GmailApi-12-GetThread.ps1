Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

# Retrieve a thread and output snippets
$thread = Get-GmailThread -GmailAccount 'user@gmail.com' -Credential $cred -Id 'threadId'
$thread.Messages | ForEach-Object { $_.Snippet }

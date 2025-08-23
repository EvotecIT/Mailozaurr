Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'access_token'

# Search for non-delivery reports in Gmail
$reports = Get-EmailDeliveryStatus -Protocol GmailApi -GmailAccount 'user@gmail.com' -Credential $cred -Since (Get-Date).AddDays(-7)

$reports | Format-Table FinalRecipient, OriginalMessageId, Type


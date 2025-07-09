Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ApiKey = 'mailgun-api-key'
$Credential = ConvertTo-MailgunCredential -ApiKey $ApiKey

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject 'Mailgun Header Test' -Body 'Hello from Mailgun' -EmailProvider Mailgun -Credential $Credential -Headers @{ 'X-Tracking-ID' = 'abc123'; 'X-Source' = 'Mailozaurr' }

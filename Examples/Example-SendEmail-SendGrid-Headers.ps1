Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ApiKey = 'sendgrid-api-key'
$Credential = ConvertTo-SendGridCredential -ApiKey $ApiKey

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject 'SendGrid Header Test' -Body 'Hello from SendGrid' -SendGrid -Credential $Credential -Headers @{ 'X-Tracking-ID' = 'abc123'; 'X-Source' = 'Mailozaurr' }

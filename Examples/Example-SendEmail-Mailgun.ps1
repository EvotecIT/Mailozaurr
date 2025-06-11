Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Use Mailgun API
$Key = Get-Content -Raw -Path "C:\Support\Important\Mailgun.txt"
$Credential = ConvertTo-MailgunCredential -ApiKey $Key

Send-EmailMessage -From 'sender@yourdomain.com' `
    -To 'recipient@example.com' `
    -Subject 'Mailgun Test' `
    -Body 'Hello from Mailgun' `
    -EmailProvider Mailgun `
    -Credential $Credential `
    -Verbose

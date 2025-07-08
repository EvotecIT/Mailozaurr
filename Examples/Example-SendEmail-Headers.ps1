Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject 'Header Test' -Body 'Hello' -Server 'smtp.example.com' -Headers @{ 'X-Tracking-ID' = 'abc123'; 'X-Custom' = 'Mailozaurr' }

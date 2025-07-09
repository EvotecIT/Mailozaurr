Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credential = Get-Credential -UserName 'AKIA...' -Message 'Enter AWS Secret Key'

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject 'SES Header Test' -Body 'Hello from SES' -Ses -Region 'us-east-1' -Credential $Credential -Headers @{ 'X-Tracking-ID' = 'abc123'; 'X-Source' = 'Mailozaurr' }

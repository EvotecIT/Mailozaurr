Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Correlate non-delivery reports with sent messages
Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential) | Out-Null
Get-EmailDeliveryMatch -Protocol Imap -SentLogPath "$env:TEMP\sendlog.json" -Recipient 'user@contoso.com'
Disconnect-IMAP

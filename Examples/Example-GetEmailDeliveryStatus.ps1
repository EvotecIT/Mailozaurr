Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Search recent non-delivery reports via IMAP
Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential) | Out-Null
Get-EmailDeliveryStatus -Protocol Imap -Recipient 'user@contoso.com' -Since (Get-Date).AddDays(-7)
Disconnect-IMAP

Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Correlate non-delivery reports with sent messages
$repo = [Mailozaurr.FileSentMessageRepository]::new("$env:TEMP\sendlog.json")
$resolver = [Mailozaurr.SendLogResolver]::new($repo)
Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential) | Out-Null
Get-EmailDeliveryMatch -Protocol Imap -Resolver $resolver -Recipient 'user@contoso.com'
Disconnect-IMAP

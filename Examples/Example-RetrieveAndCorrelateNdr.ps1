Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Retrieve recent non-delivery reports and correlate with sent messages
Connect-IMAP -Server 'imap.example.com' -Credential (Get-Credential) | Out-Null
$ndrs = Get-EmailDeliveryStatus -Protocol Imap -Since (Get-Date).AddDays(-7) -ParallelDownloadLimit 8
Get-EmailDeliveryMatch -Protocol Imap -SentLogPath "$env:TEMP\sendlog.json" -Since (Get-Date).AddDays(-7) | ForEach-Object {
    Write-Host "NDR for $($_.Report.FinalRecipient) matches message '$($_.SentMessage.Subject)'"
}
Disconnect-IMAP

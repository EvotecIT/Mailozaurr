Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $credential

Write-Host 'Waiting for new messages. Press Ctrl+C to stop.'
Wait-IMAPMessage -Client $client -Action {
    param($msg)
    Write-Host "New message: $($msg.Message.Subject)"
}

Disconnect-IMAP -Client $client

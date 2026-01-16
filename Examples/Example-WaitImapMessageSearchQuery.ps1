Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$credential = Get-Credential
$client = Connect-IMAP -Server 'imap.example.com' -Credential $credential

Write-Host 'Waiting for new IMAP messages from alice@example.com.'
Wait-IMAPMessage -Client $client -SearchQuery ([MailKit.Search.SearchQuery]::FromContains('alice@example.com')) -StopOnMatch -TimeoutSeconds 600 -Action {
    param($msg)
    Write-Host "New IMAP message from Alice: $($msg.Message.Subject)"
}

Disconnect-IMAP -Client $client

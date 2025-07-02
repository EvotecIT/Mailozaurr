Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $cred

Write-Host 'Waiting for new POP3 messages from alice@example.com.'
Wait-POP3Message -Client $client -Until {
    param($m)
    $m.Message.From.Mailboxes.Address -contains 'alice@example.com'
} -StopOnMatch -TimeoutSeconds 600 -Action {
    param($msg)
    Write-Host "New POP3 message from Alice: $($msg.Message.Subject)"
}

Disconnect-POP3 -Client $client

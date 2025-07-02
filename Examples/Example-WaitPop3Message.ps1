Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$client = Connect-POP3 -Server 'pop.example.com' -Credential $cred

Write-Host 'Waiting for new POP3 messages. Press Ctrl+C to stop.'
Wait-POP3Message -Client $client -Action {
    param($msg)
    Write-Host "New message: $($msg.Message.Subject)"
}

Disconnect-POP3 -Client $client

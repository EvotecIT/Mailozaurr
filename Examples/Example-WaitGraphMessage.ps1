Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$graph = Connect-EmailGraph -Credential $cred

Write-Host 'Waiting for new Graph messages. Press Ctrl+C to stop.'
Wait-GraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -Action {
    param($msg)
    Write-Host "New message: $($msg.subject)"
}

Disconnect-EmailGraph -Connection $graph

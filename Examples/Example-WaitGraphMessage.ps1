Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$graph = Connect-EmailGraph -Credential $cred

Write-Host 'Waiting for new Graph messages from alice@example.com or until timeout.'
Wait-GraphMessage -Connection $graph -UserPrincipalName 'user@example.com' -Until {
    param($m)
    $m.from.emailAddress.address -eq 'alice@example.com'
} -StopOnMatch -TimeoutSeconds 600 -Action {
    param($msg)
    Write-Host "New message from Alice: $($msg.subject)"
}

Disconnect-EmailGraph -Connection $graph

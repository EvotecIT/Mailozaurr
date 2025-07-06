Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

$rule = Get-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph | Where-Object DisplayName -eq 'Move Boss Mail'

if ($rule) {
    Remove-GraphInboxRule -UserPrincipalName 'user@example.com' -RuleId $rule.Id -Connection $graph -WhatIf
}

Disconnect-EmailGraph

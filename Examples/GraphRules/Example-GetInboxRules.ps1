Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

# Retrieve all rules
Get-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph | Format-Table DisplayName, Id

# Retrieve rule by display name without using Where-Object
Get-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -Filter "displayName eq 'Move Boss Mail'"

Disconnect-EmailGraph

Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

$rule = @{
    displayName = 'Move Boss Mail'
    sequence    = 1
    conditions  = @{ senderContains = @('boss@example.com') }
    actions     = @{ moveToFolder = 'Archive' }
    isEnabled   = $true
}

New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -Rule $rule

Disconnect-EmailGraph

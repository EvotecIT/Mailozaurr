Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

# Create rule from a hashtable
$rule = @{
    displayName = 'Move Boss Mail'
    sequence    = 1
    conditions  = @{ senderContains = @('boss@example.com') }
    actions     = @{ moveToFolder = 'Archive' }
    isEnabled   = $true
}
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -Rule $rule

# Create rule using a strongly typed object
$typed = New-GraphInboxRuleObject -DisplayName 'Delete Spam Subject' -Sequence 2 -SubjectContains 'spam' -Delete
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -RuleObject $typed

Disconnect-EmailGraph

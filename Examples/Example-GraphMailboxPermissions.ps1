Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Graph application credentials
$ClientId     = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId     = 'your-tenant-id'

$cred  = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

# Display existing permissions for the mailbox
$perms = Get-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com'
$perms | Format-Table Id, GrantedTo, Roles

# Grant permissions using typed objects and enum roles
$permissions = @(
    [Mailozaurr.GraphMailboxPermission]::FromHashtable(@{
        roles     = 'Owner'
        grantedTo = @{ user = 'julia@example.com' }
    }, 'user@example.com'),
    [Mailozaurr.GraphMailboxPermission]::FromHashtable(@{
        roles     = 'Read'
        grantedTo = @{ user = 'ken@example.com' }
    }, 'user@example.com')
)
Add-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com' -MailboxPermission $permissions

# Bulk add permissions from CSV (columns should map to Graph permission properties)
Add-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com' -CsvPath '.\permissions.csv'

# Review permissions after additions
Get-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com'

# Remove a single permission by id
Remove-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com' -PermissionId 'permission-id'

# Remove permissions granted to a specific user
Remove-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com' -GrantedToUser 'ken@example.com'

# Remove all owner permissions
Remove-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com' -Role Owner

# Advanced: remove using a permission object
($perms)[0].RemoveAsync($graph.Credential) | Out-Null

# Remove multiple permissions from CSV (file requires PermissionId column)
Remove-GraphMailboxPermission -Connection $graph -UserPrincipalName 'user@example.com' -CsvPath '.\permissionsToRemove.csv'

# Using Microsoft.Graph commands directly
Import-Module Microsoft.Graph.Authentication -Force
Connect-MgGraph -Scopes 'Mail.ReadWrite' -NoWelcome
Get-GraphMailboxPermission -UserPrincipalName 'user@example.com' -MgGraphRequest

Disconnect-EmailGraph -Connection $graph


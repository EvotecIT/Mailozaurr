Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null

# List permissions
Get-GraphMailboxPermission -UserPrincipalName 'user@example.com'

# Add permissions from CSV
# CSV should contain columns describing the permission body
Add-GraphMailboxPermission -UserPrincipalName 'user@example.com' -CsvPath '.\permissions.csv'

# Remove permission
Remove-GraphMailboxPermission -UserPrincipalName 'user@example.com' -PermissionId 'permission-id'

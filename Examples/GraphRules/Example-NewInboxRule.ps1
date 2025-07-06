Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred

# Create rule using typed parameters directly
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph `
    -DisplayName 'Move Boss Mail' -Sequence 1 -SenderContains 'boss@example.com' `
    -MoveToFolder 'Archive' -Enabled

# Create another rule to delete spam by subject
New-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph `
    -DisplayName 'Delete Spam Subject' -Sequence 2 -SubjectContains 'spam' -Delete

Disconnect-EmailGraph

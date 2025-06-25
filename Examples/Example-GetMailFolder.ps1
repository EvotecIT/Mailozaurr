Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# using application permissions
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
Connect-EmailGraph -Credential $cred | Out-Null
Get-MailFolder -UserPrincipalName 'user@example.com'
Disconnect-EmailGraph

# using Connect-MgGraph
Import-Module Microsoft.Graph.Authentication -Force
Connect-MgGraph -Scopes Mail.Read -NoWelcome
Get-MailFolder -UserPrincipalName 'user@example.com' -MgGraphRequest

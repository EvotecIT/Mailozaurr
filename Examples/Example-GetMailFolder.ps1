Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# using application permissions
$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred
Get-MailFolder -UserPrincipalName 'user@example.com' -Connection $graph
Disconnect-EmailGraph -Connection $graph

# using Connect-MgGraph
Import-Module Microsoft.Graph.Authentication -Force
Connect-MgGraph -Scopes Mail.Read -NoWelcome
Get-MailFolder -UserPrincipalName 'user@example.com' -MgGraphRequest

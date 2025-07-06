Import-Module $PSScriptRoot\..\..\Mailozaurr.psd1 -Force

$graph = Connect-EmailGraph -Credential (ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant')
Get-GraphInboxRule -UserPrincipalName 'user@example.com' -Connection $graph -Filter "displayName eq 'Move Boss Mail'" | Format-Table DisplayName, Id

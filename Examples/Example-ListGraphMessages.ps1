Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientId = 'your-client-id'
$ClientSecret = 'your-client-secret'
$TenantId = 'your-tenant-id'
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId
$graph = Connect-EmailGraph -Credential $cred
Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Connection $graph -Limit 5 |
    Format-Table Id, Subject, From, ReceivedDate, HasAttachments


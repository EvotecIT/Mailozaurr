# Retrieve upcoming events for a user
$cred = Connect-EmailGraph -ClientId 'client' -TenantId 'tenant' -ClientSecret 'secret'
Get-GraphEvent -UserPrincipalName 'user@example.com' -Connection $cred -Limit 5

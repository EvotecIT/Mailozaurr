# Create a simple event
$cred = Connect-EmailGraph -ClientId 'client' -TenantId 'tenant' -ClientSecret 'secret'
$builder = New-GraphEventBuilder -Subject 'Demo Meeting' -Start (Get-Date).AddHours(1) -End (Get-Date).AddHours(2) -Attendees 'user@example.com'
New-GraphEvent -UserPrincipalName 'user@example.com' -EventBuilder $builder -Connection $cred

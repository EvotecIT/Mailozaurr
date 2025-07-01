Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientId = 'Your-Application-ID'
$TenantId = 'Your-Tenant-ID'
$Scopes = @('Mail.ReadWrite','Mail.Send')

# Authenticate using device code flow
$graph = Connect-EmailGraph -ClientId $ClientId -DirectoryId $TenantId -DeviceCode -Scopes $Scopes
$graph

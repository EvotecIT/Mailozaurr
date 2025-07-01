Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientId = 'Your-Application-ID'
$TenantId = 'Your-Tenant-ID'
$ClientSecret = 'Your-Client-Secret'
$UserToken = Get-Content -Raw -Path './UserToken.txt'
$Scopes = @('Mail.ReadWrite','Mail.Send')

# Exchange existing token for Graph access
$graph = Connect-EmailGraph -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId `
    -OnBehalfOfToken $UserToken -Scopes $Scopes
$graph

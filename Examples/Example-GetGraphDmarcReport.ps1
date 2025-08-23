Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientId = 'Your-Application-ID'
$TenantId = 'Your-Tenant-ID'
$Scopes = @('Mail.Read')

Connect-EmailGraph -ClientId $ClientId -DirectoryId $TenantId -DeviceCode -Scopes $Scopes | Out-Null
Get-DmarcReport -Protocol Graph -UserPrincipalName 'user@example.com' -Since (Get-Date).AddDays(-7) |
    ForEach-Object {
        foreach ($att in $_.Attachments) {
            # Pass the zipped XML stream to Domain Detective for analysis
            # Invoke-DomainDetective -InputStream $att.Content -Name $att.Name
        }
    }
Disconnect-EmailGraph

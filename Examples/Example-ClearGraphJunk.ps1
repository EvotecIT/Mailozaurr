Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null

# Preview junk cleanup
Clear-GraphJunk -UserPrincipalName 'user@example.com' -Preview

Disconnect-EmailGraph

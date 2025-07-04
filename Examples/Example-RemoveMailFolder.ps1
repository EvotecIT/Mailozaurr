Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Remove IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto
Remove-IMAPFolder -Client $imap -Folder 'OldFolder' -WhatIf
Remove-IMAPFolder -Client $imap -Folder 'Archive/Reports' -Recursive -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Remove Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null
Remove-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -WhatIf
Remove-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'nested-id' -WhatIf
Disconnect-EmailGraph

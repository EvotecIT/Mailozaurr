Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Rename IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto
Rename-IMAPFolder -Client $imap -Folder 'OldFolder' -NewName 'NewFolder' -WhatIf
Rename-IMAPFolder -Client $imap -Folder 'Inbox/Report' -NewName 'Report-2024' -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Rename Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null
Rename-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -NewName 'New Folder' -WhatIf
Rename-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'report-id' -NewName 'Report-2024' -WhatIf
Disconnect-EmailGraph

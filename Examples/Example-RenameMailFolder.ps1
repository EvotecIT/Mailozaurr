Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Rename IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# verify current folder names before renaming
Get-IMAPFolder -Client $imap -Path 'OldFolder'

Rename-IMAPFolder -Client $imap -Folder 'OldFolder' -NewName 'NewFolder' -WhatIf

# confirm new folder name
Get-IMAPFolder -Client $imap -Path 'NewFolder'

Rename-IMAPFolder -Client $imap -Folder 'Inbox/Report' -NewName 'Report-2024' -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Rename Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null

# list folders before renaming
Get-EmailGraphFolder -UserPrincipalName 'user@example.com' | Select-Object displayName,id

Rename-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -NewName 'New Folder' -WhatIf

# verify new name
Get-EmailGraphFolder -UserPrincipalName 'user@example.com' -Connection $graphCred | Where-Object id -EQ 'folder-id'

Rename-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'report-id' -NewName 'Report-2024' -WhatIf
Disconnect-EmailGraph

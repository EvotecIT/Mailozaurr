Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Rename IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto
Rename-IMAPFolder -Client $imap -Folder 'OldFolder' -NewName 'NewFolder' -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Rename Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null
Rename-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -NewName 'New Folder' -WhatIf
Disconnect-EmailGraph

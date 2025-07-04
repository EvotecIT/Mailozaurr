Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Move IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto
Move-IMAPFolder -Client $imap -Folder 'OldFolder' -DestinationFolder 'Archive' -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Move Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null
Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -DestinationFolderId 'archive-id' -WhatIf
Disconnect-EmailGraph

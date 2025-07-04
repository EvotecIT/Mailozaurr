Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Move IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# verify folders exist before moving
Get-IMAPFolder -Client $imap -Root | Select-Object FullName
Get-IMAPFolder -Client $imap -Path 'Archive'

Move-IMAPFolder -Client $imap -Folder 'OldFolder' -DestinationFolder 'Archive' -WhatIf

# confirm destination folder after move
Get-IMAPFolder -Client $imap -Path 'Archive'

Move-IMAPFolder -Client $imap -Folder 'Sent/Reports' -DestinationFolder 'Inbox' -WhatIf
Move-IMAPFolder -Client $imap -Folder 'Drafts/Sub' -Root -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Move Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null

# verify folders before move
Get-EmailGraphFolder -UserPrincipalName 'user@example.com' | Select-Object displayName,id

Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -DestinationFolderId 'archive-id' -WhatIf

# confirm destination after move
Get-EmailGraphFolder -UserPrincipalName 'user@example.com' -Connection $graphCred | Where-Object id -EQ 'archive-id'

Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'sent-id' -DestinationFolderId 'inbox-id' -WhatIf
Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'draft-id' -Root -WhatIf
Disconnect-EmailGraph

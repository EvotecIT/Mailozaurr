Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Move IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto
Move-IMAPFolder -Client $imap -Folder 'OldFolder' -DestinationFolder 'Archive' -WhatIf
Move-IMAPFolder -Client $imap -Folder 'Sent/Reports' -DestinationFolder 'Inbox' -WhatIf
Move-IMAPFolder -Client $imap -Folder 'Drafts/Sub' -Root -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Move Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null
Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -DestinationFolderId 'archive-id' -WhatIf
Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'sent-id' -DestinationFolderId 'inbox-id' -WhatIf
Move-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'draft-id' -Root -WhatIf
Disconnect-EmailGraph

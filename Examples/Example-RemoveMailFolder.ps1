Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Remove IMAP folder
# ----------------------
$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# confirm folder presence before deletion
Get-IMAPFolder -Client $imap -Path 'OldFolder'

Remove-IMAPFolder -Client $imap -Folder 'OldFolder' -WhatIf

Remove-IMAPFolder -Client $imap -Folder 'Archive/Reports' -Recursive -WhatIf

# list archive folder after removal attempt
Get-IMAPFolder -Client $imap -Path 'Archive'
Disconnect-IMAP -Client $imap

# ----------------------
# Remove Microsoft Graph folder
# ----------------------
$graphCred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $graphCred | Out-Null

# check folder before deletion
Get-EmailGraphFolder -UserPrincipalName 'user@example.com' | Select-Object displayName,id

Remove-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'folder-id' -WhatIf

# confirm folder removed
Get-EmailGraphFolder -UserPrincipalName 'user@example.com' -Connection $graphCred | Where-Object id -EQ 'folder-id'

Remove-GraphFolder -UserPrincipalName 'user@example.com' -FolderId 'nested-id' -WhatIf
Disconnect-EmailGraph

Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ----------------------
# Basic IMAP deletion
# ----------------------
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user' -Password 'pass'
# Delete a single message by UID. -WhatIf previews the action.
Remove-IMAPMessage -Client $imap -Uid 1 -WhatIf
Disconnect-IMAP -Client $imap

# ----------------------
# Basic POP3 deletion
# ----------------------
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user' -Password 'pass'
# Remove multiple messages by index
Remove-POP3Message -Client $pop -Index 0,2 -Confirm:$false
Disconnect-POP3 -Client $pop

# ----------------------
# Basic Microsoft Graph deletion
# ----------------------
$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null
Remove-GraphMessage -UserPrincipalName 'user@example.com' -MessageId 'id' -WhatIf
Disconnect-EmailGraph

Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Remove a message via IMAP
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user' -Password 'pass'
Remove-IMAPMessage -Client $imap -Uid 1 -WhatIf
Disconnect-IMAP -Client $imap

# Remove a message via POP3
$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user' -Password 'pass'
Remove-POP3Message -Client $pop -Index 0 -WhatIf
Disconnect-POP3 -Client $pop

# Remove a message using Microsoft Graph
$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null
Remove-GraphMessage -UserPrincipalName 'user@example.com' -MessageId 'id' -WhatIf
Disconnect-EmailGraph

Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null

# Example 1: remove everything from junk
Clear-GraphJunk -UserPrincipalName 'user@example.com'

# Example 2: preview cleanup using -Preview
Clear-GraphJunk -UserPrincipalName 'user@example.com' -Preview

# Example 3: simulate cleanup with -WhatIf
Clear-GraphJunk -UserPrincipalName 'user@example.com' -WhatIf

# Example 4: skip messages from a sender
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipFrom 'boss@example.com'

# Example 5: skip messages sent to a specific recipient
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipTo 'reports@example.com'

# Example 6: skip messages containing a subject keyword
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipSubjectContains 'VIP'

# Example 7: skip specific message IDs
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipId '1','2','3'

# Example 8: combine multiple skip filters
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipFrom 'boss@example.com' -SkipSubjectContains 'Important'

# Example 9: skip messages that have any attachment
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipHasAttachment

# Example 10: skip messages containing PDF attachments
Clear-GraphJunk -UserPrincipalName 'user@example.com' -SkipAttachmentExtension 'pdf'

# Example 11: preview with additional properties
Clear-GraphJunk -UserPrincipalName 'user@example.com' -Preview -Property subject,from

# Example 12: clean junk via raw Graph requests
Clear-GraphJunk -UserPrincipalName 'user@example.com' -MgGraphRequest

Disconnect-EmailGraph

Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = Get-Credential
$imap = Connect-IMAP -Server 'imap.example.com' -Credential $cred -Port 993 -Options Auto

# Example 1: clear default junk folder
Clear-IMAPJunk -Client $imap

# Example 2: preview messages instead of removing them
Clear-IMAPJunk -Client $imap -Preview

# Example 3: simulate cleanup with -WhatIf
Clear-IMAPJunk -Client $imap -WhatIf

# Example 4: skip messages from specific sender
Clear-IMAPJunk -Client $imap -SkipFrom 'boss@example.com'

# Example 5: skip messages sent to a recipient
Clear-IMAPJunk -Client $imap -SkipTo 'reports@example.com'

# Example 6: skip messages containing subject keyword
Clear-IMAPJunk -Client $imap -SkipSubjectContains 'Important'

# Example 7: skip messages with given IDs
Clear-IMAPJunk -Client $imap -SkipMessageId '<id1@example.com>','<id2@example.com>'

# Example 8: skip messages with given UIDs
Clear-IMAPJunk -Client $imap -SkipUid 1000,1001


# Example 9: skip messages that have attachments
Clear-IMAPJunk -Client $imap -SkipHasAttachment

# Example 10: skip messages containing ZIP attachments
Clear-IMAPJunk -Client $imap -SkipAttachmentExtension 'zip'

# Example 11: combine multiple skip options
Clear-IMAPJunk -Client $imap -SkipFrom 'boss@example.com' -SkipSubjectContains 'Internal'
# Example 12: clear a custom junk folder
Clear-IMAPJunk -Client $imap -Folder 'Spam/Junk'

Disconnect-IMAP -Client $imap

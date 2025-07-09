Describe 'Clear-IMAPJunk' {
    It 'Throws when IMAP connection missing with -WhatIf and SkipFrom' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Clear-IMAPJunk -Client $info -WhatIf -SkipFrom 'a' } | Should -Throw
    }

    It 'Throws when IMAP connection missing with -Preview and SkipUid' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Clear-IMAPJunk -Client $info -Preview -SkipUid 1 } | Should -Throw
    }

    It 'Throws when IMAP connection missing with -WhatIf and SkipHasAttachment' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Clear-IMAPJunk -Client $info -WhatIf -SkipHasAttachment } | Should -Throw
    }

    It 'Throws when IMAP connection missing with -Preview and SkipAttachmentExtension' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Clear-IMAPJunk -Client $info -Preview -SkipAttachmentExtension 'zip' } | Should -Throw
    }
}

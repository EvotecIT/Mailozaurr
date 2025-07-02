Describe 'Clear-IMAPJunk' {
    It 'Warns when IMAP connection missing with -WhatIf and SkipFrom' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -WhatIf -SkipFrom 'a' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }

    It 'Warns when IMAP connection missing with -Preview and SkipUid' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -Preview -SkipUid 1 -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }

    It 'Warns when IMAP connection missing with -WhatIf and SkipHasAttachment' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -WhatIf -SkipHasAttachment -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }

    It 'Warns when IMAP connection missing with -Preview and SkipAttachmentExtension' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -Preview -SkipAttachmentExtension 'zip' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }
}

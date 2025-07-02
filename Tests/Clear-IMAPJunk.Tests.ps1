Describe 'Clear-IMAPJunk' {
    It 'Warns when IMAP connection missing with -WhatIf' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }

    It 'Warns when IMAP connection missing with -Preview' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -Preview -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }
}

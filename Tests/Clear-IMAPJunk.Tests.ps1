Describe 'Clear-IMAPJunk' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Clear-IMAPJunk -Client $info -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-IMAPJunk - Is IMAP connected?'
    }
}

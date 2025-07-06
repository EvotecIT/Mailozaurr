Describe 'Move-IMAPMessage' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Move-IMAPMessage -Client $info -Uid 1 -DestinationFolder 'Archive' -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Move-IMAPMessage - Is IMAP connected?'
    }
}

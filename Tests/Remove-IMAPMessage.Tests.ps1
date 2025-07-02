Describe 'Remove-IMAPMessage' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Remove-IMAPMessage -Client $info -Uid 1 -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-IMAPMessage - Is IMAP connected?'
    }
}

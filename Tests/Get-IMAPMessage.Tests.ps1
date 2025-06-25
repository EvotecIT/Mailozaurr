Describe 'Get-IMAPMessage' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Get-IMAPMessage -Client $info -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-IMAPMessage - Is IMAP connected?'
    }
    It 'Warns when deleting without connection' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Get-IMAPMessage -Client $info -Delete -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-IMAPMessage - Is IMAP connected?'
    }
}

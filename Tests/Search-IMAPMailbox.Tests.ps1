Describe 'Search-IMAPMailbox' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Search-IMAPMailbox -Client $info -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Search-IMAPMailbox - Is IMAP connected?'
    }
}

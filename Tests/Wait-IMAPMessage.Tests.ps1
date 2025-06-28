Describe 'Wait-IMAPMessage' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Wait-IMAPMessage -Client $info -WarningVariable warn -Action {} -ErrorAction SilentlyContinue
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Wait-IMAPMessage - Is IMAP connected?'
    }
}

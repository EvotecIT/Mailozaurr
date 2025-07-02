Describe 'Get-IMAPFolder -Root' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Get-IMAPFolder -Client $info -Root -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-IMAPFolder - Is IMAP connected?'
    }
}

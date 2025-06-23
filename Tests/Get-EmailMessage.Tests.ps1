Describe 'Get-EmailMessage' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Get-EmailMessage -ImapClient $info -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-EmailMessage - Is IMAP connected?'
    }
    It 'Warns when POP connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        Get-EmailMessage -PopClient $info -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-EmailMessage - Is POP3 connected?'
    }
    It 'Warns when deleting without connection' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Get-EmailMessage -ImapClient $info -Delete -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-EmailMessage - Is IMAP connected?'
    }
}


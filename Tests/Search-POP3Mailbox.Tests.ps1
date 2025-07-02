Describe 'Search-POP3Mailbox' {
    It 'Warns when POP3 connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        Search-POP3Mailbox -Client $info -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Search-POP3Mailbox - Is POP3 connected?'
    }
}

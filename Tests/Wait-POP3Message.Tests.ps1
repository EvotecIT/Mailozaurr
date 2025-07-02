Describe 'Wait-POP3Message' {
    It 'Warns when POP3 connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        Wait-POP3Message -Client $info -WarningVariable warn -Action {} -ErrorAction SilentlyContinue
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Wait-POP3Message - Is POP3 connected?'
    }
}

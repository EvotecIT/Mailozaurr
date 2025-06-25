Describe 'Get-POP3Message' {
    It 'Warns when POP connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        Get-POP3Message -Client $info -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-POP3Message - Is POP3 connected?'
    }
}

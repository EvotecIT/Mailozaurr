Describe 'Remove-POP3Message' {
    It 'Warns when POP3 connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        Remove-POP3Message -Client $info -Index 0 -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-POP3Message - Is POP3 connected?'
    }
}

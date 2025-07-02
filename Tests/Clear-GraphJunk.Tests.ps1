Describe 'Clear-GraphJunk' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Clear-GraphJunk -UserPrincipalName 'u' -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-GraphJunk - Connection not provided and no default session available.'
    }
}

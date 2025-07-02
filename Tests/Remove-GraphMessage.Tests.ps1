Describe 'Remove-GraphMessage' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Remove-GraphMessage -UserPrincipalName 'u' -MessageId 'id' -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-GraphMessage - Connection not provided and no default session available.'
    }
}

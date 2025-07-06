Describe 'Add-GraphMailboxPermission' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Add-GraphMailboxPermission -UserPrincipalName 'u' -Permission @{ Role = 'read' } -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Add-GraphMailboxPermission - Connection not provided and no default session available.'
    }
}

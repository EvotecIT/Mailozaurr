Describe 'Remove-GraphMailboxPermission' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Remove-GraphMailboxPermission -UserPrincipalName 'u' -PermissionId 'id' -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-GraphMailboxPermission - Connection not provided and no default session available.'
    }
}

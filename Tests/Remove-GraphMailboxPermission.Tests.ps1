Describe 'Remove-GraphMailboxPermission' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Remove-GraphMailboxPermission -UserPrincipalName 'u' -PermissionId 'id' -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-GraphMailboxPermission - Connection not provided and no default session available.'
    }

    It 'Warns when using role filter without connection' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Remove-GraphMailboxPermission -UserPrincipalName 'u' -Role Owner -WhatIf -WarningVariable warn2
        $warn2 | Should -Not -BeNullOrEmpty
        $warn2[0] | Should -Be 'Remove-GraphMailboxPermission - Connection not provided and no default session available.'
    }
}

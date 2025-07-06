Describe 'Add-GraphMailboxPermission' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Add-GraphMailboxPermission -UserPrincipalName 'u' -Permission @{ Role = 'read' } -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Add-GraphMailboxPermission - Connection not provided and no default session available.'
    }

    It 'Accepts multiple permission objects' {
        $p1 = [Mailozaurr.GraphMailboxPermission]::new()
        $p2 = [Mailozaurr.GraphMailboxPermission]::new()
        Add-GraphMailboxPermission -UserPrincipalName 'u' -MailboxPermission @($p1,$p2) -WhatIf -WarningVariable warn2
        $warn2 | Should -Not -BeNullOrEmpty
        $warn2[0] | Should -Be 'Add-GraphMailboxPermission - Connection not provided and no default session available.'
    }
}

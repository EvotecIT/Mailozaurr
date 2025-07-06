Describe 'Get-GraphMailboxPermission' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Get-GraphMailboxPermission -UserPrincipalName 'u' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-GraphMailboxPermission - Connection not provided and no default session available.'
    }
}

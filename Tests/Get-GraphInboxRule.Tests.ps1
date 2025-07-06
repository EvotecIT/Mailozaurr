Describe 'Get-GraphInboxRule' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Get-GraphInboxRule -UserPrincipalName 'u' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-GraphInboxRule - Connection not provided and no default session available.'
    }
}

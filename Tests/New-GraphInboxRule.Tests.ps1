Describe 'New-GraphInboxRule' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        New-GraphInboxRule -UserPrincipalName 'u' -Rule @{displayName='x'} -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'New-GraphInboxRule - Connection not provided and no default session available.'
    }
}

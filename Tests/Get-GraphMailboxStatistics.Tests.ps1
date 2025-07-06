Describe 'Get-GraphMailboxStatistics' {
    It 'Warns when Graph connection missing' {
        Get-GraphMailboxStatistics -UserPrincipalName 'u' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-GraphMailboxStatistics - Connection not provided and no default session available.'
    }
}

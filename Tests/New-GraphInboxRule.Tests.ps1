Describe 'New-GraphInboxRule' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        New-GraphInboxRule -UserPrincipalName 'u' -Rule @{displayName='x'} -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'New-GraphInboxRule - Connection not provided and no default session available.'
    }

    It 'Warns when connection missing with builder' {
        $b = New-GraphInboxRuleBuilder -DisplayName 'Test' -Sequence 1 -SenderContains 'a@example.com'
        New-GraphInboxRule -UserPrincipalName 'u' -RuleBuilder $b -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'New-GraphInboxRule - Connection not provided and no default session available.'
    }
}

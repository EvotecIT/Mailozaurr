Describe 'Set-GraphInboxRule' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Set-GraphInboxRule -UserPrincipalName 'u' -RuleId 'id' -Rule @{displayName='x'} -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Set-GraphInboxRule - Connection not provided and no default session available.'
    }
}

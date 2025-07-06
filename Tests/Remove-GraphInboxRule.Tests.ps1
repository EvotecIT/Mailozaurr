Describe 'Remove-GraphInboxRule' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Remove-GraphInboxRule -UserPrincipalName 'u' -RuleId 'id' -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-GraphInboxRule - Connection not provided and no default session available.'
    }
}

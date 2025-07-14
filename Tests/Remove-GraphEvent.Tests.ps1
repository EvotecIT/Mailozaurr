Describe 'Remove-GraphEvent' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Remove-GraphEvent -UserPrincipalName 'u' -EventId 'id' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Remove-GraphEvent - Connection not provided and no default session available.'
    }
}

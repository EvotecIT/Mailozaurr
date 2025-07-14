Describe 'Get-GraphEvent' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Get-GraphEvent -UserPrincipalName 'u' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-GraphEvent - Connection not provided and no default session available.'
    }
}

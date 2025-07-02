Describe 'Wait-GraphMessage' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Wait-GraphMessage -Connection $info -UserPrincipalName 'u' -WarningVariable warn -Action {} -ErrorAction SilentlyContinue
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Wait-GraphMessage - Connection not provided and no default session available.'
    }
}

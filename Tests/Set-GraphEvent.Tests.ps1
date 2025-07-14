Describe 'Set-GraphEvent' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        $ev = [Mailozaurr.GraphEvent]::new()
        Set-GraphEvent -UserPrincipalName 'u' -EventId 'id' -Event $ev -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Set-GraphEvent - Connection not provided and no default session available.'
    }
}

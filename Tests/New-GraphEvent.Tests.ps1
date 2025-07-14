Describe 'New-GraphEvent' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        $ev = [Mailozaurr.GraphEvent]::new()
        New-GraphEvent -UserPrincipalName 'u' -Event $ev -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'New-GraphEvent - Connection not provided and no default session available.'
    }

    It 'Warns when connection missing with builder' {
        $b = New-GraphEventBuilder -Subject 't' -Start (Get-Date) -End (Get-Date)
        New-GraphEvent -UserPrincipalName 'u' -EventBuilder $b -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'New-GraphEvent - Connection not provided and no default session available.'
    }
}

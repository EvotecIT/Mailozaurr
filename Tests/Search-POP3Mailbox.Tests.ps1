Describe 'Search-POP3Mailbox' {
    It 'Throws when POP3 connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        { Search-POP3Mailbox -Client $info } | Should -Throw
    }

    It 'Supports BodyContains parameter' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        { Search-POP3Mailbox -Client $info -BodyContains 'test' } | Should -Throw
    }
}

Describe 'Search-IMAPMailbox' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Search-IMAPMailbox -Client $info } | Should -Throw
    }

    It 'Supports BodyContains parameter' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Search-IMAPMailbox -Client $info -BodyContains 'test' } | Should -Throw
    }
}

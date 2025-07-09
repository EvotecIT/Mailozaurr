Describe 'Search-IMAPMailbox' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Search-IMAPMailbox -Client $info } | Should -Throw
    }
}

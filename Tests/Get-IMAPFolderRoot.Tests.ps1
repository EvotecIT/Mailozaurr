Describe 'Get-IMAPFolder -Root' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPFolder -Client $info -Root } | Should -Throw
    }
}

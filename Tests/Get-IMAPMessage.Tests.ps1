Describe 'Get-IMAPMessage' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPMessage -Client $info } | Should -Throw
    }
    It 'Throws when deleting without connection' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPMessage -Client $info -Delete } | Should -Throw
    }
}

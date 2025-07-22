Describe 'Get-IMAPMessage' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPMessage -Client $info } | Should -Throw
    }
    It 'Throws when deleting without connection' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPMessage -Client $info -Delete } | Should -Throw
    }
    It 'Throws when mixing UID and sequence parameters' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPMessage -Client $info -SequenceStart 1 -UidStart 1 } | Should -Throw
    }
}

Describe 'Remove-IMAPMessage' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Remove-IMAPMessage -Client $info -Uid 1 -WhatIf } | Should -Throw
    }
}

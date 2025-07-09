Describe 'Move-IMAPMessage' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Move-IMAPMessage -Client $info -Uid 1 -DestinationFolder 'Archive' -WhatIf } | Should -Throw
    }
}

Describe 'Get-IMAPMessage -Delete with WhatIf' {
    It 'Throws when deleting without connection' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Get-IMAPMessage -Client $info -Delete -WhatIf } | Should -Throw
    }
}

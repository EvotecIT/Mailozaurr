Describe 'Remove-POP3Message' {
    It 'Throws when POP3 connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        { Remove-POP3Message -Client $info -Index 0 -WhatIf } | Should -Throw
    }
}

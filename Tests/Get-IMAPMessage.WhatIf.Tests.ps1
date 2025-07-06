Describe 'Get-IMAPMessage -Delete with WhatIf' {
    It 'Warns when deleting without connection' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Get-IMAPMessage -Client $info -Delete -WhatIf -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Get-IMAPMessage - Is IMAP connected?'
    }
}

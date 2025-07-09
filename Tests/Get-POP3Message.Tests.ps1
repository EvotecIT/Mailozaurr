Describe 'Get-POP3Message' {
    It 'Throws when POP connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        { Get-POP3Message -Client $info } | Should -Throw
    }
}

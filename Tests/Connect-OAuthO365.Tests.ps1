Describe 'Connect-OAuthO365 cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletConnectOAuthO365].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }
}


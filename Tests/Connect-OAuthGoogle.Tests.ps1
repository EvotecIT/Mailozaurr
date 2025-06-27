Describe 'Connect-OAuthGoogle cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletConnectOAuthGoogle].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }
}

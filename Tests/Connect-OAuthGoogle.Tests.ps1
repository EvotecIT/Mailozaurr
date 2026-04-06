Describe 'Connect-OAuthGoogle cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletConnectOAuthGoogle].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }

    It 'exposes ClientSecretSecureString as a SecureString parameter' {
        $command = Get-Command Connect-OAuthGoogle

        $command.Parameters.ContainsKey('ClientSecretSecureString') | Should -BeTrue
        $command.Parameters['ClientSecretSecureString'].ParameterType | Should -Be ([securestring])
    }

    It 'exposes ClientSecretSecretName for vault-based resolution' {
        $command = Get-Command Connect-OAuthGoogle

        $command.Parameters.ContainsKey('ClientSecretSecretName') | Should -BeTrue
        $command.Parameters['ClientSecretSecretName'].ParameterType | Should -Be ([string])
    }

    It 'exposes ClientSecretVaultName for vault selection' {
        $command = Get-Command Connect-OAuthGoogle

        $command.Parameters.ContainsKey('ClientSecretVaultName') | Should -BeTrue
        $command.Parameters['ClientSecretVaultName'].ParameterType | Should -Be ([string])
    }
}

Describe 'Connect-EmailGraph cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletConnectEmailGraph].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }

    It 'exposes ClientSecretSecureString as a SecureString parameter' {
        $command = Get-Command Connect-EmailGraph

        $command.Parameters.ContainsKey('ClientSecretSecureString') | Should -BeTrue
        $command.Parameters['ClientSecretSecureString'].ParameterType | Should -Be ([securestring])
    }

    It 'exposes CertificatePasswordSecureString as a SecureString parameter' {
        $command = Get-Command Connect-EmailGraph

        $command.Parameters.ContainsKey('CertificatePasswordSecureString') | Should -BeTrue
        $command.Parameters['CertificatePasswordSecureString'].ParameterType | Should -Be ([securestring])
    }

    It 'exposes OnBehalfOfTokenSecureString as a SecureString parameter' {
        $command = Get-Command Connect-EmailGraph

        $command.Parameters.ContainsKey('OnBehalfOfTokenSecureString') | Should -BeTrue
        $command.Parameters['OnBehalfOfTokenSecureString'].ParameterType | Should -Be ([securestring])
    }

    It 'exposes ClientSecretSecretName for vault-based client secret resolution' {
        $command = Get-Command Connect-EmailGraph

        $command.Parameters.ContainsKey('ClientSecretSecretName') | Should -BeTrue
        $command.Parameters['ClientSecretSecretName'].ParameterType | Should -Be ([string])
    }

    It 'exposes CertificatePasswordSecretName for vault-based certificate password resolution' {
        $command = Get-Command Connect-EmailGraph

        $command.Parameters.ContainsKey('CertificatePasswordSecretName') | Should -BeTrue
        $command.Parameters['CertificatePasswordSecretName'].ParameterType | Should -Be ([string])
    }

    It 'exposes OnBehalfOfTokenSecretName for vault-based token resolution' {
        $command = Get-Command Connect-EmailGraph

        $command.Parameters.ContainsKey('OnBehalfOfTokenSecretName') | Should -BeTrue
        $command.Parameters['OnBehalfOfTokenSecretName'].ParameterType | Should -Be ([string])
    }
}

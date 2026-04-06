Describe 'ConvertTo-GraphCertificateCredential cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletConvertToGraphCertificateCredential].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }

    It 'exposes CertificatePasswordSecureString as a SecureString parameter' {
        $command = Get-Command ConvertTo-GraphCertificateCredential

        $command.Parameters.ContainsKey('CertificatePasswordSecureString') | Should -BeTrue
        $command.Parameters['CertificatePasswordSecureString'].ParameterType | Should -Be ([securestring])
    }

    It 'exposes SecretName for vault-based certificate password resolution' {
        $command = Get-Command ConvertTo-GraphCertificateCredential

        $command.Parameters.ContainsKey('SecretName') | Should -BeTrue
        $command.Parameters['SecretName'].ParameterType | Should -Be ([string])
    }

    It 'exposes VaultName for vault selection' {
        $command = Get-Command ConvertTo-GraphCertificateCredential

        $command.Parameters.ContainsKey('VaultName') | Should -BeTrue
        $command.Parameters['VaultName'].ParameterType | Should -Be ([string])
    }
}

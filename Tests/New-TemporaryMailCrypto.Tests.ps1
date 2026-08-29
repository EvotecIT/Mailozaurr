Describe 'New-TemporaryMailCrypto cmdlet' {
    It 'Creates PGP keys with custom path' {
        $dir = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
        $keys = New-TemporaryMailCrypto -Pgp -OutputPath $dir -NoDispose
        Test-Path $keys.PublicKeyPath | Should -BeTrue
        $keys.Dispose()
        Test-Path $keys.PublicKeyPath | Should -BeTrue
        Remove-Item $dir -Recurse -Force
    }
    It 'Creates S/MIME certificate and saves to file' {
        $path = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName() + '.pfx')
        $passwordText = 'mailozaurr-test-password'
        $password = ConvertTo-SecureString $passwordText -AsPlainText -Force
        $cert = New-TemporaryMailCrypto -Smime -OutputPath $path -OutputPassword $password
        $cert | Should -BeOfType ([System.Security.Cryptography.X509Certificates.X509Certificate2])
        Test-Path $path | Should -BeTrue
        $loaded = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($path, $passwordText)
        $loaded.HasPrivateKey | Should -BeTrue
        $loaded.Dispose()
        $cert.Dispose()
        Remove-Item $path -Force
    }

    It 'Exposes the S/MIME output password as SecureString' {
        $command = Get-Command New-TemporaryMailCrypto
        $command.Parameters['OutputPassword'].ParameterType | Should -Be ([securestring])
    }
}

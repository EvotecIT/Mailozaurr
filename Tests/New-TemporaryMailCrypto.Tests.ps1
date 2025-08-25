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
        $cert = New-TemporaryMailCrypto -Smime -OutputPath $path
        $cert | Should -BeOfType ([System.Security.Cryptography.X509Certificates.X509Certificate2])
        Test-Path $path | Should -BeTrue
        Remove-Item $path -Force
    }
}

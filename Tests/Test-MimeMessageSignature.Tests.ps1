Describe 'Test-MimeMessageSignature' {
    It 'Returns false for unsigned message' {
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Subject = 'plain'
        $result = $msg | Test-MimeMessageSignature
        $result | Should -Be $false
    }

    It 'Verifies PGP signature' {
        $base = Join-Path $PSScriptRoot '..'
        $pub = Join-Path $base 'Examples/PGPKeys/mimekit.gpg.pub'
        $sec = Join-Path $base 'Examples/PGPKeys/mimekit.gpg.sec'
        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'a@b.com'
        $smtp.To = @('b@c.com')
        $smtp.Subject = 't'
        $smtp.TextBody = 'body'
        $smtp.CreateMessage()
        $smtp.PgpSign($pub,$sec,'no.secret',$false) | Out-Null
        $msg = $smtp.Message
        $result = $msg | Test-MimeMessageSignature -PublicKeyPath $pub
        $result | Should -Be $true
    }

    It 'Verifies SMIME signature' {
        $rsa = [System.Security.Cryptography.RSA]::Create(2048)
        $req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('cn=test',$rsa,[System.Security.Cryptography.HashAlgorithmName]::SHA256,[System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $cert = $req.CreateSelfSigned([System.DateTimeOffset]::Now.AddDays(-1),[System.DateTimeOffset]::Now.AddDays(1))
        $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'x@y.com'
        $smtp.To = @('z@a.com')
        $smtp.Subject = 's'
        $smtp.TextBody = 'text'
        $smtp.CreateMessage()
        $smtp.Sign($cert) | Out-Null
        $msg = $smtp.Message
        $result = $msg | Test-MimeMessageSignature -Certificate $cert
        $result | Should -Be $true
    }
}

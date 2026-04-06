Describe 'Test-MimeMessageSignature' {
    It 'Returns false for unsigned message' {
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Subject = 'plain'
        $result = $msg | Test-MimeMessageSignature
        $result | Should -Be $false
    }

    It 'Verifies PGP signature' -Skip:(-not $IsWindows) {
        $base = Join-Path $PSScriptRoot '..'
        $pub = Join-Path $base 'Examples/PGPKeys/mimekit.gpg.pub'
        $sec = Join-Path $base 'Examples/PGPKeys/mimekit.gpg.sec'
        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'mimekit@example.com'
        $smtp.To = @('b@c.com')
        $smtp.Subject = 't'
        $smtp.TextBody = 'body'
        $smtp.CreateMessage()
        $smtp.PgpSign($pub,$sec,'no.secret',$false) | Out-Null
        $msg = $smtp.Message
        $result = $msg | Test-MimeMessageSignature -PublicKeyPath $pub
        $result | Should -Be $true
    }

    It 'Verifies SMIME signature' -Skip:(-not $IsWindows) {
        $cert = [Mailozaurr.TemporarySmimeCertificate]::CreateSelfSigned('CN=test@example.com')
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

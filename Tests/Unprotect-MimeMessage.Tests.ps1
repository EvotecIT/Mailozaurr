Describe 'Unprotect-MimeMessage' {
    It 'Returns original message when not encrypted' {
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Subject = 'plain'
        $result = $msg | Unprotect-MimeMessage
        $result | Should -Be $msg
    }

    It 'Decrypts PGP message' {
        $base = Join-Path $PSScriptRoot '..'
        $pub = Join-Path $base 'Examples/PGPKeys/mimekit.gpg.pub'
        $sec = Join-Path $base 'Examples/PGPKeys/mimekit.gpg.sec'
        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'a@b.com'
        $smtp.To = @('b@c.com')
        $smtp.Subject = 't'
        $smtp.TextBody = 'body'
        $smtp.CreateMessage()
        $smtp.PgpEncrypt($pub) | Out-Null
        $msg = $smtp.Message
        $out = $msg | Unprotect-MimeMessage -PrivateKeyPath $sec -PrivateKeyPassword 'no.secret'
        ($out.TextBody) | Should -Be 'body'
    }

    It 'Decrypts SMIME message' -Skip:(-not $IsWindows) {
        $cert = [Mailozaurr.TemporarySmimeCertificate]::CreateSelfSigned('CN=test@example.com')
        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'x@y.com'
        $smtp.To = @('z@a.com')
        $smtp.Subject = 'enc'
        $smtp.TextBody = 'data'
        $smtp.CreateMessage()
        $smtp.Encrypt($cert) | Out-Null
        $msg = $smtp.Message
        $out = $msg | Unprotect-MimeMessage -Certificate $cert
        ($out.TextBody) | Should -Be 'data'
    }

    It 'Handles wrapper objects' {
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Subject = 'plain'
        $imap = [Mailozaurr.ImapEmailMessage]::new([MailKit.UniqueId]::new(1), $msg)
        $info = [Mailozaurr.ImapMessageInfo]::new($imap)
        $out1 = Unprotect-MimeMessage -InputObject $imap
        $out2 = Unprotect-MimeMessage -InputObject $info
        $graph = [Mailozaurr.GraphEmailMessage]::new('1', $msg)
        $out3 = Unprotect-MimeMessage -InputObject $graph
        $out1.Subject | Should -Be $msg.Subject
        $out2.Subject | Should -Be $msg.Subject
        $out3.Subject | Should -Be $msg.Subject
    }
}

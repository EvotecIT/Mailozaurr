Describe 'Send-EmailMessage - PGP' {
    It 'Signs and encrypts via PGP (WhatIf)' {
        $pub = Join-Path $PSScriptRoot '..\Examples\PGPKeys\mimekit.gpg.pub'
        $sec = Join-Path $PSScriptRoot '..\Examples\PGPKeys\mimekit.gpg.sec'
        $result = Send-EmailMessage -From 'mimekit@example.com' -To 'mimekit@example.com' -Server 'smtp.example.com' -Port 25 -Body 'test' -Subject 'test' -WhatIf `
            -SignOrEncrypt PgpSignAndEncrypt -PublicKeyPath $pub -PrivateKeyPath $sec -PrivateKeyPassword 'no.secret'
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }

    It 'Returns error when key files are missing' {
        $smtp = [Mailozaurr.Smtp]::new()
        $smtp.From = 'a@b.com'
        $smtp.To = @('c@d.com')
        $smtp.Subject = 'Test'
        $smtp.TextBody = 'Body'
        $smtp.CreateMessage()
        $result = $smtp.PgpSignAndEncrypt('missing.pub', 'missing.sec', '', $false)
        $result.Error | Should -Match 'file not found'
        $result.Status | Should -Be $false
    }
}

Describe 'Send-EmailMessage - PGP' {
    It 'Signs and encrypts via PGP (WhatIf)' {
        $pub = Join-Path $PSScriptRoot '..\Examples\PGPKeys\mimekit.gpg.pub'
        $sec = Join-Path $PSScriptRoot '..\Examples\PGPKeys\mimekit.gpg.sec'
        $result = Send-EmailMessage -From 'mimekit@example.com' -To 'mimekit@example.com' -Server 'smtp.example.com' -Port 25 -Body 'test' -Subject 'test' -WhatIf `
            -SignOrEncrypt PgpSignAndEncrypt -PublicKeyPath $pub -PrivateKeyPath $sec -PrivateKeyPassword 'no.secret'
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

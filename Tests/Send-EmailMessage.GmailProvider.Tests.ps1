Describe 'Send-EmailMessage - Gmail provider' {
    It 'Enumeration includes Gmail' {
        [enum]::GetNames([Mailozaurr.EmailProvider]) | Should -Contain 'Gmail'
    }
    It 'WhatIf returns not sent' {
        $cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'tok'
        $res = Send-EmailMessage -EmailProvider Gmail -GmailAccount 'user@gmail.com' -From 'user@gmail.com' -To 'a@test.com' -Credential $cred -WhatIf
        $res.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

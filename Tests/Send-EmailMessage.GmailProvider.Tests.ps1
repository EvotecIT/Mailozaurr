Describe 'Send-EmailMessage - Gmail provider' {
    It 'Enumeration includes Gmail' {
        [enum]::GetNames([Mailozaurr.EmailProvider]) | Should -Contain 'Gmail'
    }
    It 'WhatIf returns result' {
        $cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'tok'
        { Send-EmailMessage -EmailProvider Gmail -GmailAccount 'user@gmail.com' -From 'user@gmail.com' -To 'a@test.com' -Subject 's' -Body 'b' -Credential $cred -WhatIf } | Should -Not -Throw
    }
}

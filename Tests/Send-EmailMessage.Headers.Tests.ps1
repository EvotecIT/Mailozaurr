Describe 'Send-EmailMessage - Headers' {
    It 'Accepts custom headers parameter' {
        $result = Send-EmailMessage -From 'a@b.com' -To 'c@d.com' -Subject 't' -Body 'b' -Server 'smtp.example.com' -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }

    It 'Accepts custom headers with Gmail provider' {
        $cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'tok'
        $result = Send-EmailMessage -EmailProvider Gmail -GmailAccount 'user@gmail.com' -From 'user@gmail.com' -To 'recipient@example.com' -Subject 't' -Body 'b' -Credential $cred -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

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

    It 'Accepts custom headers with Graph provider' {
        $cred = ConvertTo-GraphCredential -ClientID 'client' -ClientSecret 'secret' -DirectoryID 'tenant'
        $result = Send-EmailMessage -From 'user@example.com' -To 'recipient@example.com' -Subject 't' -Body 'b' -Graph -Credential $cred -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }

    It 'Accepts custom headers with SendGrid provider' {
        $cred = ConvertTo-SendGridCredential -ApiKey 'key'
        $result = Send-EmailMessage -From 'user@example.com' -To 'recipient@example.com' -Subject 't' -Body 'b' -SendGrid -Credential $cred -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }

    It 'Accepts custom headers with Mailgun provider' {
        $cred = ConvertTo-MailgunCredential -ApiKey 'key'
        $result = Send-EmailMessage -From 'user@example.com' -To 'recipient@example.com' -Subject 't' -Body 'b' -EmailProvider Mailgun -Credential $cred -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }

    It 'Accepts custom headers with SES provider' {
        $secure = ConvertTo-SecureString 'secret' -AsPlainText -Force
        $cred = [PSCredential]::new('access', $secure)
        $result = Send-EmailMessage -From 'user@example.com' -To 'recipient@example.com' -Subject 't' -Body 'b' -EmailProvider SES -Region 'us-east-1' -Credential $cred -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

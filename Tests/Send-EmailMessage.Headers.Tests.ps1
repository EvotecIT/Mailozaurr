Describe 'Send-EmailMessage - Headers' {
    It 'Accepts custom headers parameter' {
        $result = Send-EmailMessage -From 'a@b.com' -To 'c@d.com' -Subject 't' -Body 'b' -Server 'smtp.example.com' -Headers @{ 'X-Test'='123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

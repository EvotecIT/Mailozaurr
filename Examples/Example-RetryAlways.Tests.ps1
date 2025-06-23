Describe 'RetryAlways example' {
    It 'Uses RetryAlways switch' {
        $params = @{ 
            From = 'test@contoso.com'
            To = 'recipient@contoso.com'
            Server = 'smtp.example.com'
            Subject = 'Test'
            Text = 'Hello'
            RetryAlways = $true
            RetryCount = 2
            WhatIf = $true
        }
        $result = Send-EmailMessage @params -ErrorAction Stop
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

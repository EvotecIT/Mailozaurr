Describe 'Send-EmailMessage - Authentication Skipped' {
    It 'Skips authentication when no credentials provided' {
        $result = Send-EmailMessage -From 'noauth@example.com' -To 'recipient@example.com' -Subject 'Skip Auth Test' -Body 'test' -Server 'smtp.example.com' -Port 25 -WhatIf -Verbose 4>&1
        $output = $result | Where-Object { $_ -isnot [System.Management.Automation.VerboseRecord] }
        $verbose = $result | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] }
        ($verbose | Select-Object -ExpandProperty Message) | Should -Contain 'Send-EmailMessage - Skipping authentication'
        $output.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

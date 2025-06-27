Describe 'Send-EmailMessage - Attachment Cleanup' {
    It 'Warns and skips missing attachment paths' {
        $missing1 = 'mailozaurr_missing1.txt'
        $missing2 = 'mailozaurr_missing2.txt'
        if (Test-Path $missing1) { Remove-Item $missing1 -Force }
        if (Test-Path $missing2) { Remove-Item $missing2 -Force }
        $result = Send-EmailMessage -From 'a@b.com' -To 'c@d.com' -Subject 'x' -Body 'y' -Server 'smtp.example.com' -Port 25 -Attachment $missing1 -InlineAttachment $missing2 -WarningAction Continue -WhatIf 3>&1
        $warnings = $result | Where-Object { $_ -is [System.Management.Automation.WarningRecord] }
        $warnings.Count | Should -Be 2
        ($warnings[0].Message + $warnings[1].Message) | Should -Match 'mailozaurr_missing1.txt'
        ($warnings[0].Message + $warnings[1].Message) | Should -Match 'mailozaurr_missing2.txt'
    }
}

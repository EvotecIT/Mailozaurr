Import-Module (Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1") -Force

Describe 'Send-EmailMessage - Async Logging' {
    It 'does not raise thread-bound Write warnings' {
        $warnings = @()
        Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Server 'smtp.example.com' `
            -Subject 'Test' -Text 'Body' -Port 25 -WarningVariable warnings -WhatIf
        $warnings | Should -Not -Contain 'WriteObject and WriteError methods cannot be called'
    }
}

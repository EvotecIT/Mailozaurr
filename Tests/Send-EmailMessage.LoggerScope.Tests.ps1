Import-Module (Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1") -Force

Describe 'Send-EmailMessage - Logger scope' {
    It 'restores previous logger after execution' {
        $original = [Mailozaurr.LoggingMessages]::Logger
        Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Server 'smtp.example.com' \
            -Subject 'Test' -Text 'Body' -Port 25 -WhatIf
        [Mailozaurr.LoggingMessages]::Logger | Should -Be $original
    }
}

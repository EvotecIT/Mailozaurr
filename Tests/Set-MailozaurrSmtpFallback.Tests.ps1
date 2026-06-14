Import-Module $PSScriptRoot/../Mailozaurr.psd1 -Force

Describe 'Set-MailozaurrSmtpFallback' {
    It 'Clears configured fallback without requiring direct option access' {
        { Set-MailozaurrSmtpFallback -Clear } | Should -Not -Throw
    }
}

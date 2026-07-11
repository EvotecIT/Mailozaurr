Describe 'Import-Module' {
    BeforeAll {
        $modulePath = Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1"
        Remove-Module Mailozaurr -Force -ErrorAction SilentlyContinue
        $script:module = Import-Module $modulePath -Force -PassThru -ErrorAction Stop
    }

    It 'imports the manifest successfully' {
        $module | Should -Not -BeNullOrEmpty
        $module.Name | Should -Be 'Mailozaurr'
    }

    It 'exports representative commands after import' {
        (Get-Command Get-SmtpConnectionPool -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        (Get-Command Export-MailFile -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        (Get-Command Import-MailFile -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        (Get-Command Send-EmailMessage -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
    }
}

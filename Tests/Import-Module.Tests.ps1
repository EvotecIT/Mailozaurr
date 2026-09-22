Describe 'Import-Module' {
    BeforeAll {
        $modulePath = Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1"
        $script:manifest = Import-PowerShellDataFile -Path $modulePath
        Remove-Module Mailozaurr -Force -ErrorAction SilentlyContinue
        $script:module = Import-Module $modulePath -Force -PassThru -ErrorAction Stop
    }

    It 'imports the manifest successfully' {
        $module | Should -Not -BeNullOrEmpty
        $module.Name | Should -Be 'Mailozaurr'
    }

    It 'exports representative commands after import' {
        $manifest.CmdletsToExport | Should -Contain 'Export-MailFile'
        (Get-Command Get-SmtpConnectionPool -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        (Get-Command Export-MailFile -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        (Get-Command Import-MailFile -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        (Get-Command Send-EmailMessage -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        foreach ($name in @(
            'Compare-MailDataSemantic'
            'Get-MailSemanticFingerprint'
            'Get-MailStoreConversation'
            'Get-MailStoreMaintenancePlan'
            'Search-MailAddressBook'
        )) {
            $manifest.CmdletsToExport | Should -Contain $name
            (Get-Command $name -ErrorAction Stop).ModuleName | Should -Be 'Mailozaurr'
        }
        [OfficeIMO.Email.EmailSemanticComparisonOptions]::new() | Should -Not -BeNullOrEmpty
        [OfficeIMO.Email.Store.EmailConversationGraphOptions]::new() | Should -Not -BeNullOrEmpty
    }
}

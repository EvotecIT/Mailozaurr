Describe 'Get-SmtpConnectionPool' {
    BeforeAll { Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force }

    It 'Returns empty snapshot for new pool' {
        [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($true)
        [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
        $snapshot = Get-SmtpConnectionPool
        $snapshot.CurrentPoolSize | Should -Be 0
        $snapshot.Entries.Count | Should -Be 0
        [Mailozaurr.SmtpConnectionPool]::SetPoolingEnabled($false)
    }
}

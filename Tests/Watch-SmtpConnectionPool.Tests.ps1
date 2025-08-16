Describe 'Watch-SmtpConnectionPool' {
    BeforeAll { Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force }

    It 'Registers watcher for pool updates' {
        [Mailozaurr.SmtpConnectionPool]::PoolingEnabled = $true
        [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
        $values = [System.Collections.Generic.List[int]]::new()
        $watcher = Watch-SmtpConnectionPool -Action { param($s) $values.Add($s.CurrentPoolSize) }
        [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
        [Mailozaurr.SmtpConnectionPool]::remove_PoolSizeChanged($watcher)
        [Mailozaurr.SmtpConnectionPool]::PoolingEnabled = $false
        $values.Count | Should -BeGreaterThan 0
    }
}

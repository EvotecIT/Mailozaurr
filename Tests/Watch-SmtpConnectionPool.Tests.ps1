Describe 'Watch-SmtpConnectionPool' {
    BeforeAll { Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force }

    It 'Registers watcher for pool updates' {
        [Mailozaurr.SmtpConnectionPool]::PoolingEnabled = $true
        [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
        $values = [System.Collections.Generic.List[int]]::new()
        $watcher = Watch-SmtpConnectionPool -Action { param($s) $values.Add($s.CurrentPoolSize) }
        [Mailozaurr.SmtpConnectionPool]::PoolSizeChanged.Invoke(0)
        Unregister-Event -SubscriptionId $watcher.Id
        [Mailozaurr.SmtpConnectionPool]::PoolingEnabled = $false
        $values.Count | Should -BeGreaterThan 0
    }
}

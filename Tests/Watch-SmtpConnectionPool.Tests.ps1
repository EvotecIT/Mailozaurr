Describe 'Watch-SmtpConnectionPool' {
    BeforeAll { Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force }

    It 'Registers watcher for pool updates' {
        Clear-SmtpConnectionPool -Confirm:$false
        $values = [System.Collections.Generic.List[int]]::new()
        $watcher = Watch-SmtpConnectionPool -Action { param($s) $values.Add($s.CurrentPoolSize) }
        Clear-SmtpConnectionPool -Confirm:$false
        Unregister-SmtpConnectionPoolWatcher -Watcher $watcher
        $values.Count | Should -BeGreaterThan 0
    }
}

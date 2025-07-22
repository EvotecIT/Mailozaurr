Describe 'Wait-POP3Message' {
    It 'Throws when POP3 connection missing' {
        $info = [Mailozaurr.PowerShell.PopConnectionInfo]::new()
        { Wait-POP3Message -Client $info -Action {} } | Should -Throw
    }

    It 'Invokes action and cancels on match' {
        $cmd = [Mailozaurr.PowerShell.CmdletWaitPOP3Message]::new()
        $script:wasCalled = $false
        $cmd.Action = { $script:wasCalled = $true }
        $cmd.Until = { $true }
        $cmd.StopOnMatch = $true
        $method = $cmd.GetType().GetMethod('OnMessageArrived', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        $msg = [Mailozaurr.Pop3EmailMessage]::new(0, [MimeKit.MimeMessage]::new())
        $method.Invoke($cmd, @($null, $msg))
        $wasCalled | Should -Be $true
        $field = $cmd.GetType().BaseType.GetField('_cancelSource', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        ($field.GetValue($cmd)).IsCancellationRequested | Should -Be $true
    }

    It 'Dispose can be called multiple times' {
        $cmd = [Mailozaurr.PowerShell.CmdletWaitPOP3Message]::new()
        $cmd.Dispose()
        { $cmd.Dispose() } | Should -Not -Throw
    }
}

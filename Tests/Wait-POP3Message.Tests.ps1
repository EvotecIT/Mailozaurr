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
        $matchSource = [System.Threading.CancellationTokenSource]::new()
        $matchField = $cmd.GetType().GetField('_matchSource', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        $matchField.SetValue($cmd, $matchSource)
        $method = $cmd.GetType().GetMethod('OnMessageArrived', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        $msg = [Mailozaurr.Pop3EmailMessage]::new(0, [MimeKit.MimeMessage]::new())
        $method.Invoke($cmd, @($null, $msg))
        $wasCalled | Should -Be $true
        $matchSource.IsCancellationRequested | Should -Be $true
        $stopField = $cmd.GetType().BaseType.GetField('_cancelSource', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        ($stopField.GetValue($cmd)).IsCancellationRequested | Should -Be $false
    }

    It 'Dispose can be called multiple times' {
        $cmd = [Mailozaurr.PowerShell.CmdletWaitPOP3Message]::new()
        $cmd.Dispose()
        { $cmd.Dispose() } | Should -Not -Throw
    }
}

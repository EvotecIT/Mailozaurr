Describe 'Wait-IMAPMessage' {
    It 'Throws when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        { Wait-IMAPMessage -Client $info -Action {} } | Should -Throw
    }

    It 'Invokes action and cancels on match' {
        $cmd = [Mailozaurr.PowerShell.CmdletWaitIMAPMessage]::new()
        $script:wasCalled = $false
        $cmd.Action = { $script:wasCalled = $true }
        $cmd.Until = { $true }
        $cmd.StopOnMatch = $true
        $method = $cmd.GetType().GetMethod('OnMessageArrived', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        $msg = [Mailozaurr.ImapEmailMessage]::new([MailKit.UniqueId]::MinValue, [MimeKit.MimeMessage]::new())
        $method.Invoke($cmd, @($null, $msg))
        $wasCalled | Should -Be $true
        $field = $cmd.GetType().BaseType.GetField('_cancelSource', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        ($field.GetValue($cmd)).IsCancellationRequested | Should -Be $true
    }

    It 'Dispose can be called multiple times' {
        $cmd = [Mailozaurr.PowerShell.CmdletWaitIMAPMessage]::new()
        $cmd.Dispose()
        { $cmd.Dispose() } | Should -Not -Throw
    }
}

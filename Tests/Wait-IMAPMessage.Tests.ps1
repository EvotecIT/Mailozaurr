Describe 'Wait-IMAPMessage' {
    It 'Warns when IMAP connection missing' {
        $info = [Mailozaurr.PowerShell.ImapConnectionInfo]::new()
        Wait-IMAPMessage -Client $info -WarningVariable warn -Action {} -ErrorAction SilentlyContinue
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Wait-IMAPMessage - Is IMAP connected?'
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
}

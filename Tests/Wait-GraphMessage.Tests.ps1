Describe 'Wait-GraphMessage' {
    It 'Warns when Graph connection missing' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Wait-GraphMessage -Connection $info -UserPrincipalName 'u' -WarningVariable warn -Action {} -ErrorAction SilentlyContinue
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Wait-GraphMessage - Connection not provided and no default session available.'
    }

    It 'Invokes action and cancels on match' {
        $cmd = [Mailozaurr.PowerShell.CmdletWaitGraphMessage]::new()
        $script:wasCalled = $false
        $cmd.Action = { $script:wasCalled = $true }
        $cmd.Until = { $true }
        $cmd.StopOnMatch = $true
        $method = $cmd.GetType().GetMethod('OnMessageArrived', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        $msg = [System.Collections.Generic.Dictionary[string,object]]::new()
        $msg.Add('id','1')
        $method.Invoke($cmd, @($null, $msg))
        $wasCalled | Should -Be $true
        $field = $cmd.GetType().BaseType.GetField('_cancelSource', [System.Reflection.BindingFlags] 'NonPublic, Instance')
        ($field.GetValue($cmd)).IsCancellationRequested | Should -Be $true
    }
}

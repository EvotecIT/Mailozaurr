Describe 'Send-GmailMessage cmdlet' {
    It 'Cmdlet derives from AsyncPSCmdlet' {
        $base = [Mailozaurr.PowerShell.CmdletSendGmailMessage].BaseType
        $base.FullName | Should -Be 'Mailozaurr.PowerShell.AsyncPSCmdlet'
    }

    It 'Accepts custom headers parameter' {
        $cred = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -Token 'tok'
        $result = Send-GmailMessage -GmailAccount 'user@gmail.com' -Credential $cred -From 'user@gmail.com' -To 'recipient@example.com' -Subject 'h' -TextBody 'b' -Headers @{ 'X-Test' = '123' } -WhatIf
        $result.Error | Should -Be 'Email not sent (WhatIf)'
    }
}

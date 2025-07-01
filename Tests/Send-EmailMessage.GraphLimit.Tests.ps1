Describe 'Send-EmailMessage - Graph Size Limit' {
    It 'Returns error when attachments exceed Graph limit' {
        $file = [System.IO.Path]::GetTempFileName()
        [byte[]]$bytes = New-Object byte[] 150000001
        [System.IO.File]::WriteAllBytes($file, $bytes)
        $cred = New-Object System.Management.Automation.PSCredential('a@b.com', (ConvertTo-SecureString 'x' -AsPlainText -Force))
        $result = Send-EmailMessage -From 'a@b.com' -To 'c@d.com' -Subject 's' -Body 'b' -Graph -Attachment $file -Credential $cred -WhatIf 2>&1
        Remove-Item $file
        $error = $result | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }
        $error.Exception.Message | Should -Match '150MB'
    }
}

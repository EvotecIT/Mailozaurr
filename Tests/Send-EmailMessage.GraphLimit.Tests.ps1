Describe 'Send-EmailMessage - Graph Size Limit' {
    It 'Returns error when attachments exceed Graph limit' {
        $file = [System.IO.Path]::GetTempFileName()
        try {
            $stream = [System.IO.File]::OpenWrite($file)
            try { $stream.SetLength(150000001) } finally { $stream.Dispose() }
            $cred = New-Object System.Management.Automation.PSCredential('a@b.com', (ConvertTo-SecureString 'x' -AsPlainText -Force))
            $result = Send-EmailMessage -From 'a@b.com' -To 'c@d.com' -Subject 's' -Body 'b' -Graph -Attachment $file -Credential $cred -WhatIf -ErrorAction Continue 2>&1
            $error = $result | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }
            $error.Exception.Message | Should -Match '150MB'
        } finally {
            Remove-Item -LiteralPath $file
        }
    }
}

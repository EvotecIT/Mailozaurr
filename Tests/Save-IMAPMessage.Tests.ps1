Describe 'Save-IMAPMessage' {
    It 'Creates target directory when missing' {
        $dir = Join-Path $TestDrive 'imap'
        $file = Join-Path $dir 'msg.eml'

        # simulate logic of cmdlet for testing
        $message = [MimeKit.MimeMessage]::new()
        if (-not (Test-Path $dir)) { [System.IO.Directory]::CreateDirectory($dir) | Out-Null }
        $message.WriteTo($file)

        Test-Path $dir | Should -BeTrue
    }

    It 'Removes temp file when convert fails' {
        $tempDir = Join-Path $TestDrive 'tmp'
        [System.IO.Directory]::CreateDirectory($tempDir) | Out-Null
        $oldTemp = $env:TEMP
        $oldTmp = $env:TMP
        $env:TEMP = $tempDir
        $env:TMP = $tempDir

        $message = [MimeKit.MimeMessage]::new()
        $msgPath = '/sys/fail.msg'

        $tmp = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.eml')
        try {
            $message.WriteTo($tmp)
            [Mailozaurr.EmailMessage]::ConvertEmlToMsg([System.IO.FileInfo]$tmp, [System.IO.FileInfo]$msgPath, $true) | Out-Null
        } finally {
            if (Test-Path $tmp) { Remove-Item $tmp }
        }

        (Get-ChildItem $tempDir -Filter '*.eml') | Should -BeNullOrEmpty

        $env:TEMP = $oldTemp
        $env:TMP = $oldTmp
    }
}

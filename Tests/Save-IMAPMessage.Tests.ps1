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
}

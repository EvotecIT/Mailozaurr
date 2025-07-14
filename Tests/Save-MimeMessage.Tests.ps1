Describe 'Save-MimeMessage' {
    It 'Saves to EML file' {
        $msg = [MimeKit.MimeMessage]::new()
        $file = Join-Path $TestDrive 'test.eml'
        Save-MimeMessage -InputObject $msg -Path $file
        Test-Path $file | Should -BeTrue
    }

    It 'Converts to MSG' {
        $msg = [MimeKit.MimeMessage]::new()
        $file = Join-Path $TestDrive 'test.msg'
        Save-MimeMessage -InputObject $msg -Path $file
        Test-Path $file | Should -BeTrue
    }
}

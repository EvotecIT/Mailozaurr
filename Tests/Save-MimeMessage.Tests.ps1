Describe 'Save-MimeMessage' {
    It 'Saves to EML file' {
        $msg = New-MimeMessage -From 'sender@example.invalid' -To 'reader@example.invalid' -Subject 'Save test' -TextBody 'Local fixture'
        $file = Join-Path $TestDrive 'test.eml'
        Save-MimeMessage -InputObject $msg -Path $file
        Test-Path $file | Should -BeTrue
    }

    It 'Converts to MSG' {
        $msg = New-MimeMessage -From 'sender@example.invalid' -To 'reader@example.invalid' -Subject 'Save test' -TextBody 'Local fixture'
        $file = Join-Path $TestDrive 'test.msg'
        Save-MimeMessage -InputObject $msg -Path $file
        Test-Path $file | Should -BeTrue
    }

    It 'Saves relative to the PowerShell location when the process directory differs' {
        $message = New-MimeMessage -From 'sender@example.invalid' -To 'reader@example.invalid' -Subject 'PowerShell location' -TextBody 'Local fixture'
        $processDirectory = [Environment]::CurrentDirectory
        Push-Location $TestDrive
        try {
            Save-MimeMessage -InputObject $message -Path './nested/message.eml'
            $file = Join-Path $TestDrive 'nested/message.eml'
            Test-Path -LiteralPath $file | Should -BeTrue
            Get-Content -LiteralPath $file -Raw | Should -Match '(?m)^Subject: PowerShell location\r?$'
            [Environment]::CurrentDirectory | Should -Be $processDirectory
        } finally { Pop-Location }
    }

    It 'Resolves a custom filesystem drive for the destination' {
        $message = New-MimeMessage -From 'sender@example.invalid' -To 'reader@example.invalid' -Subject 'Save test' -TextBody 'Local fixture'
        $null = New-PSDrive -Name MessageOutput -PSProvider FileSystem -Root $TestDrive
        try {
            Save-MimeMessage -InputObject $message -Path 'MessageOutput:/message.eml'
            Test-Path -LiteralPath (Join-Path $TestDrive 'message.eml') | Should -BeTrue
        } finally { Remove-PSDrive -Name MessageOutput }
    }
}

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

    It 'Rejects <Path> without writing a provider path as a filesystem file' -TestCases @(
        @{ Path = 'Env:message.eml'; FileName = 'message.eml'; Existing = $true }
        @{ Path = 'Variable:message.eml'; FileName = 'message.eml'; Existing = $true }
        @{ Path = 'Microsoft.PowerShell.Core\Environment::message.eml'; FileName = 'message.eml'; Existing = $true }
        @{ Path = 'Env:unexpected.eml'; FileName = 'unexpected.eml'; Existing = $false }
    ) {
        param($Path, $FileName, $Existing)
        $message = New-MimeMessage -From 'sender@example.invalid' -To 'reader@example.invalid' -Subject 'Save test' -TextBody 'Local fixture'
        $victim = Join-Path $TestDrive $FileName
        if ($Existing) { [IO.File]::WriteAllText($victim, 'Preserve this file') }
        $processDirectory = [Environment]::CurrentDirectory
        Push-Location $TestDrive
        try {
            [Environment]::CurrentDirectory = $TestDrive
            { Save-MimeMessage -InputObject $message -Path $Path -ErrorAction Stop } | Should -Throw '*FileSystem*'
            if ($Existing) {
                [IO.File]::ReadAllText($victim) | Should -Be 'Preserve this file'
            } else {
                Test-Path -LiteralPath $victim | Should -BeFalse
            }
        } finally {
            [Environment]::CurrentDirectory = $processDirectory
            Pop-Location
        }
    }
}

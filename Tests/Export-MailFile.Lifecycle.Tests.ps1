Describe 'Export-MailFile input ownership' {
    BeforeAll {
        $script:previousDevelopmentMode = $env:MAILOZAURR_DEVELOPMENT
        $env:MAILOZAURR_DEVELOPMENT = '1'
        $modulePath = Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1"
        Remove-Module Mailozaurr -Force -ErrorAction SilentlyContinue
        Import-Module $modulePath -Force -ErrorAction Stop

        $script:NewTestEml = {
            param([string] $Path)

            @(
                'From: Alice <alice@example.com>'
                'To: Bob <bob@example.com>'
                'Subject: Export ownership'
                'Date: Mon, 21 Jun 2021 10:00:00 +0000'
                'Message-ID: <export-ownership@example.com>'
                'MIME-Version: 1.0'
                'Content-Type: text/plain; charset=utf-8'
                ''
                'Body'
            ) -join "`r`n" | Set-Content -LiteralPath $Path -NoNewline
        }
    }

    AfterAll {
        $env:MAILOZAURR_DEVELOPMENT = $script:previousDevelopmentMode
    }

    BeforeEach {
        $script:message = $null
        $script:sourcePath = Join-Path $TestDrive 'source.eml'
        & $script:NewTestEml -Path $script:sourcePath
    }

    AfterEach {
        if ($null -ne $script:message -and -not $script:message.IsDisposed) {
            $script:message.Dispose()
        }
    }

    It 'disposes a successfully exported pipeline input by default' {
        $script:message = Import-MailFile -InputPath $script:sourcePath
        $outputPath = Join-Path $TestDrive 'exported.msg'

        $script:message | Export-MailFile -OutputPath $outputPath

        $script:message.IsDisposed | Should -BeTrue
        $outputPath | Should -Exist
    }

    It 'keeps a successfully exported input open when requested' {
        $script:message = Import-MailFile -InputPath $script:sourcePath
        $outputPath = Join-Path $TestDrive 'kept-open.msg'

        $script:message | Export-MailFile -OutputPath $outputPath -KeepInputOpen

        $script:message.IsDisposed | Should -BeFalse
        $outputPath | Should -Exist
    }

    It 'disposes the input when export exits early for an existing destination' {
        $script:message = Import-MailFile -InputPath $script:sourcePath
        $outputPath = Join-Path $TestDrive 'existing.msg'
        Set-Content -LiteralPath $outputPath -Value 'existing'

        $errors = @()
        $script:message | Export-MailFile -OutputPath $outputPath -ErrorAction SilentlyContinue -ErrorVariable errors

        $script:message.IsDisposed | Should -BeTrue
        $errors.FullyQualifiedErrorId | Should -Contain 'MailFileAlreadyExists,Mailozaurr.PowerShell.CmdletExportMailFile'
    }
}

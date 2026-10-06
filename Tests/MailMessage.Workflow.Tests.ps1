Describe 'Message-centric local email workflows' {
    BeforeAll {
        $modulePath = if ($env:MAILOZAURR_TEST_MODULE_PATH) { $env:MAILOZAURR_TEST_MODULE_PATH } else { Join-Path $PSScriptRoot '../Mailozaurr.psd1' }
        Import-Module $modulePath -Force -ErrorAction Stop
        $script:source = Join-Path $TestDrive 'mail'
        New-Item -ItemType Directory -Path $script:source | Out-Null
        $script:eml = Join-Path $script:source 'invoice.eml'
        $script:png = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jf0sAAAAASUVORK5CYII='
        $script:body = @(
            'From: Alice <alice@example.test>'
            'To: Bob <bob@example.test>'
            'Subject: Invoice 2026'
            'Date: Thu, 01 Oct 2026 12:00:00 +0000'
            'Message-ID: <invoice@example.test>'
            'MIME-Version: 1.0'
            'Content-Type: multipart/mixed; boundary="mixed"'
            ''
            '--mixed'
            'Content-Type: multipart/alternative; boundary="alternative"'
            ''
            '--alternative'
            'Content-Type: text/plain; charset=utf-8'
            ''
            'Invoice body'
            '--alternative'
            'Content-Type: text/html; charset=utf-8'
            ''
            '<p>Invoice body</p><img src="cid:logo">'
            '--alternative--'
            '--mixed'
            'Content-Type: image/png; name="logo.png"'
            'Content-Disposition: inline; filename="logo.png"'
            'Content-ID: <logo>'
            'Content-Transfer-Encoding: base64'
            ''
            $script:png
            '--mixed'
            'Content-Type: text/plain; name="invoice.txt"'
            'Content-Disposition: attachment; filename="invoice.txt"'
            'Content-Transfer-Encoding: base64'
            ''
            'aW52b2ljZSBhdHRhY2htZW50'
            '--mixed--'
        ) -join "`r`n"
        [IO.File]::WriteAllText($script:eml, $script:body)
        $other = Join-Path $script:source 'Other'
        New-Item -ItemType Directory -Path $other | Out-Null
        [IO.File]::WriteAllText((Join-Path $other 'project.eml'), $script:body.Replace('Subject: Invoice 2026', 'Subject: Project update'))
        $script:pst = Join-Path $TestDrive 'archive.pst'
        ConvertTo-MailPst -InputPath $script:source -OutputPath $script:pst -SkipVerification -ErrorAction Stop | Out-Null
        $script:ost = Join-Path $TestDrive 'cache.ost'
        $ostFixture = Join-Path $PSScriptRoot 'Assets/SyntheticMailStore.ost.b64'
        [IO.File]::WriteAllBytes($script:ost, [Convert]::FromBase64String([IO.File]::ReadAllText($ostFixture)))
    }

    It 'returns readable bodies and metadata without an open source handle' {
        $mail = Get-MailMessage $script:eml -ErrorAction Stop
        $mail.Subject | Should -Be 'Invoice 2026'
        $mail.TextBody.Trim() | Should -Be 'Invoice body'
        $mail.HtmlBody | Should -Match 'cid:logo'
        $mail.From.Address | Should -Be 'alice@example.test'
        @($mail.Attachments).Count | Should -Be 2
        $stream = [IO.File]::Open($script:eml, 'Open', 'ReadWrite', 'None')
        $stream.Dispose()
    }

    It 'filters attachment descriptions and saves their exact bytes and provenance' {
        $mail = Get-MailMessage $script:eml -ErrorAction Stop
        $image = $mail | Get-MailAttachment -InlineOnly -ContentType 'image/*'
        $image.FileName | Should -Be 'logo.png'
        $saved = $image | Save-MailAttachment (Join-Path $TestDrive 'images') -PassThru -ErrorAction Stop
        [Convert]::ToBase64String([IO.File]::ReadAllBytes($saved.Path)) | Should -Be $script:png
        $saved.SourcePath | Should -Be $script:eml
        $saved.OriginalFileName | Should -Be 'logo.png'
        $mail | Get-MailAttachment -FileName '*.txt','*.pdf' | ForEach-Object FileName | Should -Be 'invoice.txt'
    }

    It 'uses PST views after the scoped store has closed' {
        $mail = Invoke-MailStore $script:pst {
            param($store)
            $store | Get-MailMessage -SubjectContains 'invoice' -First 1 -ErrorAction Stop
        }
        $mail.TextBody.Trim() | Should -Be 'Invoice body'
        $saved = $mail | Save-MailAttachment (Join-Path $TestDrive 'pst-files') -FileName '*.txt' -PassThru -ErrorAction Stop
        [IO.File]::ReadAllText($saved.Path) | Should -Be 'invoice attachment'
        $copy = $mail | Export-MailFile (Join-Path $TestDrive 'portable.eml') -AllowLoss -PassThru -ErrorAction Stop
        $copy.Exists | Should -BeTrue
        $mail.Subject | Should -Be 'Invoice 2026'
        $readBack = Get-MailMessage $copy.FullName -ErrorAction Stop
        $readBack.Subject | Should -Be $mail.Subject
    }

    It 'closes a scoped reader after a terminating script error' {
        { Invoke-MailStore $script:pst { throw 'expected failure' } -ErrorAction Stop } | Should -Throw
        $stream = [IO.File]::Open($script:pst, 'Open', 'ReadWrite', 'None')
        $stream.Dispose()
    }

    It 'reads an OST message and exposes its offline cache availability' {
        $mail = Get-MailMessage $script:ost -First 1 -ErrorAction Stop
        $mail.Subject | Should -Be 'Synthetic PST message'
        $mail.TextBody | Should -Match 'PST property context'
        $mail.ContentAvailability.IsPotentiallyPartial | Should -BeTrue
    }

    It 'reports incomplete searches instead of hiding the scan limit' {
        $found = @(Get-MailMessage $script:pst -SubjectContains 'does not match' -MaxItemsScanned 1 -WarningVariable warnings -WarningAction SilentlyContinue -ErrorAction Stop)
        $found.Count | Should -Be 0
        $warnings.Count | Should -Be 1
        $warnings[0].ToString() | Should -Match 'MaxItemsScanned'
    }

    It 'creates local HTML resources and preserves the input view for subsequent export' {
        $mail = Get-MailMessage $script:eml -ErrorAction Stop
        $copy = $mail | Export-MailFile (Join-Path $TestDrive 'invoice.html') -IncludeAttachments -PassThru -ErrorAction Stop
        $html = [IO.File]::ReadAllText($copy.FullName)
        $html | Should -Not -Match 'cid:logo'
        $html | Should -Match 'invoice\.assets-'
        $html | Should -Match 'invoice.txt'
        $mail | Export-MailFile (Join-Path $TestDrive 'invoice.msg') -ErrorAction Stop
        $mail.TextBody.Trim() | Should -Be 'Invoice body'
    }

    It 'honors WhatIf without creating destination directories' {
        $mail = Get-MailMessage $script:eml -ErrorAction Stop
        $destination = Join-Path $TestDrive 'preview'
        $mail | Save-MailAttachment $destination -WhatIf
        $mail | Export-MailFile -OutputDirectory $destination -Format Html -WhatIf
        Test-Path -LiteralPath $destination | Should -BeFalse
    }

    It 'applies First across wildcard inputs and gives batch exports distinct names' {
        $second = Join-Path $script:source 'invoice-two.eml'
        [IO.File]::WriteAllText($second, $script:body)
        $pattern = Join-Path $script:source '*.eml'
        @(Get-MailMessage $pattern -First 1 -ErrorAction Stop).Count | Should -Be 1
        $messages = Get-MailMessage $pattern -ErrorAction Stop
        $copies = @($messages | Export-MailFile -OutputDirectory (Join-Path $TestDrive 'batch') -Format Eml -PassThru -ErrorAction Stop)
        $copies.Count | Should -Be 2
        @($copies.Name | Select-Object -Unique).Count | Should -Be 2
        $files = @($messages | Save-MailAttachment (Join-Path $TestDrive 'flat') -Flatten -FileName '*.txt' -PassThru -ErrorAction Stop)
        $files.Count | Should -Be 2
        @($files.Path | Select-Object -Unique).Count | Should -Be 2
    }
}

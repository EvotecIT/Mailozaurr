Describe 'OfficeIMO.Email mail-store workflows' {
    BeforeAll {
        $script:previousDevelopmentMode = $env:MAILOZAURR_DEVELOPMENT
        if ([string]::IsNullOrWhiteSpace($env:MAILOZAURR_TEST_MODULE_PATH)) {
            $env:MAILOZAURR_DEVELOPMENT = '1'
            $modulePath = Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1"
        } else {
            $modulePath = Resolve-Path $env:MAILOZAURR_TEST_MODULE_PATH
        }
        Remove-Module Mailozaurr -Force -ErrorAction SilentlyContinue
        Import-Module $modulePath -Force -ErrorAction Stop

        $script:WriteEml = {
            param(
                [string] $Path,
                [string] $Subject,
                [string] $Body,
                [string] $MessageId
            )

            @(
                'From: Alice <alice@example.com>'
                'To: Bob <bob@example.com>'
                "Subject: $Subject"
                'Date: Mon, 21 Jun 2021 10:00:00 +0000'
                "Message-ID: <$MessageId>"
                'MIME-Version: 1.0'
                'Content-Type: text/plain; charset=utf-8'
                ''
                $Body
            ) -join "`r`n" | Set-Content -LiteralPath $Path -NoNewline
        }

        $script:sourceOne = Join-Path $TestDrive 'source-one'
        $script:sourceTwo = Join-Path $TestDrive 'source-two'
        New-Item -ItemType Directory -Path $script:sourceOne, $script:sourceTwo | Out-Null
        & $script:WriteEml (Join-Path $script:sourceOne 'invoice.eml') 'Invoice 2026' 'Renewal contract body' 'invoice@example.test'
        & $script:WriteEml (Join-Path $script:sourceOne 'project.eml') 'Project update' 'Budget planning body' 'project@example.test'
        & $script:WriteEml (Join-Path $script:sourceTwo 'support.eml') 'Support handoff' 'Customer support body' 'support@example.test'

        $script:pstOne = Join-Path $TestDrive 'source-one.pst'
        $script:pstTwo = Join-Path $TestDrive 'source-two.pst'
        # Build raw MIME fixtures without the optional semantic comparison, then
        # exercise verified PST-to-PST conversion against normalized data below.
        $script:conversionOne = ConvertTo-MailPst $script:sourceOne $script:pstOne -SkipVerification -ErrorAction Stop
        $script:conversionTwo = ConvertTo-MailPst $script:sourceTwo $script:pstTwo -SkipVerification -ErrorAction Stop
        $script:data = Import-MailData $script:pstOne -ErrorAction Stop
    }

    AfterAll {
        if ($null -ne $script:data) {
            $script:data | Close-MailData
        }
        $env:MAILOZAURR_DEVELOPMENT = $script:previousDevelopmentMode
    }

    It 'exports the complete archive command family' {
        $commands = @(
            'Import-MailData'
            'Close-MailData'
            'Get-MailStoreFolder'
            'Get-MailStoreItem'
            'Search-MailStore'
            'Test-MailStore'
            'Export-MailStore'
            'ConvertTo-MailPst'
            'Merge-MailStore'
        )

        foreach ($command in $commands) {
            Get-Command $command -Module Mailozaurr -ErrorAction Stop | Should -Not -BeNullOrEmpty
        }
    }

    It 'creates Unicode PST output from mailbox directories' {
        $script:pstOne | Should -Exist
        $script:pstTwo | Should -Exist
        $script:conversionOne.SourceFormat.ToString() | Should -Be 'MailboxDirectory'
        $script:conversionOne.ConvertedItems | Should -Be 2
        $script:conversionOne.Verification | Should -BeNullOrEmpty
        $script:conversionTwo.ConvertedItems | Should -Be 1
    }

    It 'verifies a normalized PST conversion before committing it' {
        $verifiedPath = Join-Path $TestDrive 'verified-copy.pst'
        $report = ConvertTo-MailPst $script:pstOne $verifiedPath -ErrorAction Stop

        $verifiedPath | Should -Exist
        $report.Verification.IsSuccessful | Should -BeTrue
        $report.Verification.MatchedItems | Should -Be 2
    }

    It 'opens PST data through the unified owner and exposes folders and references' {
        $script:data.Kind.ToString() | Should -Be 'Store'
        $script:data.Store.Format.ToString() | Should -Be 'Pst'
        @($script:data | Get-MailStoreFolder).Count | Should -BeGreaterThan 0
        @($script:data | Get-MailStoreItem -MaxItems 10).Count | Should -Be 2
    }

    It 'selectively reads native OfficeIMO store items' {
        $items = @($script:data | Get-MailStoreItem -Read -Parts Metadata,Bodies -MaxItems 10)

        $items.Count | Should -Be 2
        @($items | ForEach-Object { $_.Document.Subject }) | Should -Contain 'Invoice 2026'
        @($items | ForEach-Object { $_.Document.Body.Text }) | Should -Contain 'Renewal contract body'
    }

    It 'supports bounded metadata and content search' {
        $metadata = @($script:data | Search-MailStore -SubjectContains 'invoice' -MaxResults 10)
        $content = @($script:data | Search-MailStore -Term 'renewal','contract' -MatchMode AllTerms -ResultsOnly)

        $metadata.Count | Should -Be 1
        $metadata[0].Summary.Subject | Should -Be 'Invoice 2026'
        $content.Count | Should -Be 1
        $content[0].Summary.Subject | Should -Be 'Invoice 2026'
        $content[0].Snippet | Should -Match 'Renewal contract'
    }

    It 'returns a complete native validation report' {
        $report = $script:data | Test-MailStore -VerifyStructuralIntegrity -MaxItems 10

        $report.IsValid | Should -BeTrue
        $report.IsComplete | Should -BeTrue
        $report.ItemsExamined | Should -Be 2
        $report.StructuralIntegritySupported | Should -BeTrue
    }

    It 'exports message directories and aggregate or native mailbox formats' {
        $emlDirectory = Join-Path $TestDrive 'eml-export'
        $mboxPath = Join-Path $TestDrive 'export.mbox'
        $maildirPath = Join-Path $TestDrive 'maildir-export'
        $emlxPath = Join-Path $TestDrive 'emlx-export'

        $eml = $script:data | Export-MailStore $emlDirectory -Format Eml -Flatten -ErrorAction Stop
        $mbox = $script:data | Export-MailStore $mboxPath -Format Mbox -ErrorAction Stop
        $maildir = $script:data | Export-MailStore $maildirPath -Format Maildir -Flatten -ErrorAction Stop
        $emlx = $script:data | Export-MailStore $emlxPath -Format Emlx -Flatten -ErrorAction Stop

        $eml.SucceededCount | Should -Be 2
        $mbox.SucceededCount | Should -Be 2
        $maildir.SucceededCount | Should -Be 2
        $emlx.SucceededCount | Should -Be 2
        $mboxPath | Should -Exist

        $mboxData = Import-MailData $mboxPath -ErrorAction Stop
        try {
            $mboxData.Store.Format.ToString() | Should -Be 'Mbox'
            @($mboxData | Get-MailStoreItem).Count | Should -Be 2
        } finally {
            $mboxData | Close-MailData
        }
    }

    It 'merges multiple read-only stores into one PST' {
        $mergedPath = Join-Path $TestDrive 'merged.pst'
        $report = Merge-MailStore $script:pstOne, $script:pstTwo $mergedPath -ErrorAction Stop

        $mergedPath | Should -Exist
        $report.WrittenItems | Should -Be 3
        $report.SkippedItems | Should -Be 0
    }

    It 'does not create export, conversion, or merge output under WhatIf' {
        $exportPath = Join-Path $TestDrive 'whatif-export'
        $conversionPath = Join-Path $TestDrive 'whatif-conversion.pst'
        $mergePath = Join-Path $TestDrive 'whatif-merge.pst'

        $script:data | Export-MailStore $exportPath -Format Eml -WhatIf
        ConvertTo-MailPst $script:pstOne $conversionPath -WhatIf
        Merge-MailStore $script:pstOne, $script:pstTwo $mergePath -WhatIf

        $exportPath | Should -Not -Exist
        $conversionPath | Should -Not -Exist
        $mergePath | Should -Not -Exist
    }

    It 'discovers standalone calendar and contact artifacts' {
        $icsPath = Join-Path $TestDrive 'meeting.ics'
        $vcfPath = Join-Path $TestDrive 'contact.vcf'
        @(
            'BEGIN:VCALENDAR'
            'VERSION:2.0'
            'BEGIN:VEVENT'
            'UID:meeting@example.test'
            'DTSTART:20260720T090000Z'
            'SUMMARY:Planning'
            'END:VEVENT'
            'END:VCALENDAR'
        ) -join "`r`n" | Set-Content -LiteralPath $icsPath -NoNewline
        @(
            'BEGIN:VCARD'
            'VERSION:4.0'
            'FN:Ada Lovelace'
            'EMAIL:ada@example.test'
            'END:VCARD'
        ) -join "`r`n" | Set-Content -LiteralPath $vcfPath -NoNewline

        $calendar = Import-MailData $icsPath -ErrorAction Stop
        $contact = Import-MailData $vcfPath -ErrorAction Stop
        try {
            $calendar.Kind.ToString() | Should -Be 'Calendar'
            $calendar.Calendar.GetComponents('VEVENT').Count | Should -Be 1
            $contact.Kind.ToString() | Should -Be 'Contact'
            $contact.Contact.Cards.Count | Should -Be 1
        } finally {
            $calendar | Close-MailData
            $contact | Close-MailData
        }
    }

    It 'closes imported store ownership explicitly' {
        $owned = Import-MailData $script:pstTwo -ErrorAction Stop
        $owned | Close-MailData

        { $owned | Get-MailStoreItem -ErrorAction Stop } | Should -Throw
    }
}

Describe 'Export-GraphMailboxToPst' {
    It 'warns and writes nothing when no Graph connection is available' {
        $outputPath = Join-Path $TestDrive 'no-connection'

        Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath $outputPath -WarningVariable warn -WarningAction SilentlyContinue

        $warn[0] | Should -Be 'Export-GraphMailboxToPst - Connection not provided and no default session available.'
        $outputPath | Should -Not -Exist
    }

    It 'does not contact Graph or create files under WhatIf' {
        $connection = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        $outputPath = Join-Path $TestDrive 'whatif'
        New-Item -ItemType Directory -Path $outputPath | Out-Null

        Export-GraphMailboxToPst -Identity 'user@contoso.com' -OutputPath $outputPath -SplitSizeMB 1024 -Connection $connection -WhatIf -ErrorAction Stop

        @(Get-ChildItem -LiteralPath $outputPath).Count | Should -Be 0
    }
}

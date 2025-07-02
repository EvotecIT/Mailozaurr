Describe 'Clear-GraphJunk' {
    It 'Warns when Graph connection missing with -WhatIf and SkipId' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Clear-GraphJunk -UserPrincipalName 'u' -WhatIf -SkipId '1' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-GraphJunk - Connection not provided and no default session available.'
    }

    It 'Warns when Graph connection missing with -Preview and SkipFrom' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Clear-GraphJunk -UserPrincipalName 'u' -Preview -SkipFrom 'a' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-GraphJunk - Connection not provided and no default session available.'
    }

    It 'Warns when Graph connection missing with -WhatIf and SkipHasAttachment' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Clear-GraphJunk -UserPrincipalName 'u' -WhatIf -SkipHasAttachment -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-GraphJunk - Connection not provided and no default session available.'
    }

    It 'Warns when Graph connection missing with -Preview and SkipAttachmentExtension' {
        $info = [Mailozaurr.PowerShell.GraphConnectionInfo]::new()
        Clear-GraphJunk -UserPrincipalName 'u' -Preview -SkipAttachmentExtension 'pdf' -WarningVariable warn
        $warn | Should -Not -BeNullOrEmpty
        $warn[0] | Should -Be 'Clear-GraphJunk - Connection not provided and no default session available.'
    }
}

Describe 'Move-GraphMessage' {
    It 'Skips execution when using WhatIf' {
        Move-GraphMessage -UserPrincipalName 'u' -MessageId 'id' -DestinationFolderId 'dest' -MgGraphRequest -WhatIf -ErrorVariable err
        $err | Should -BeNullOrEmpty
    }
}

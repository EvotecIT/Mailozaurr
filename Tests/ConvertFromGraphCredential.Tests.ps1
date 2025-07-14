Describe 'MicrosoftGraphUtils.ConvertFromGraphCredential' {
    It 'Parses "client@tenant" credential' {
        $cred = [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('client@tenant', 'secret')
        $cred.ClientId | Should -Be 'client'
        $cred.DirectoryId | Should -Be 'tenant'
        $cred.ClientSecret | Should -Be 'secret'
    }

    It 'Parses credential with whitespace' {
        $cred = [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('  client@tenant  ', 'secret')
        $cred.ClientId | Should -Be 'client'
        $cred.DirectoryId | Should -Be 'tenant'
        $cred.ClientSecret | Should -Be 'secret'
    }
    It 'Throws for invalid format' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('invalid', 'pwd') } | Should -Throw
    }

    It 'Throws for invalid format with multiple @' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('client@tenant@other', 'pwd') } | Should -Throw
    }
}

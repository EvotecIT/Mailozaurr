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
    It 'Throws when username is null' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential($null, 'secret') } | Should -Throw -ExceptionType System.ArgumentNullException
    }
    It 'Throws when username is whitespace' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('   ', 'secret') } | Should -Throw -ExceptionType System.ArgumentException
    }
    It 'Throws when password is null' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('client@tenant', $null) } | Should -Throw -ExceptionType System.ArgumentNullException
    }
    It 'Throws when password is whitespace' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('client@tenant', '   ') } | Should -Throw -ExceptionType System.ArgumentException
    }
    It 'Throws for invalid format' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('invalid', 'pwd') } | Should -Throw
    }

    It 'Throws for invalid format with multiple @' {
        { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('client@tenant@other', 'pwd') } | Should -Throw
    }
}

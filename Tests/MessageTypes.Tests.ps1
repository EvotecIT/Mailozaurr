Describe 'Message wrapper types' {
    It 'Pop3EmailMessage type is available' {
        [Mailozaurr.Pop3EmailMessage] | Should -Not -BeNullOrEmpty
    }

    It 'ImapEmailMessage type is available' {
        [Mailozaurr.ImapEmailMessage] | Should -Not -BeNullOrEmpty
    }
}

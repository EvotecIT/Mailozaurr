Describe 'Message wrapper types' {
    It 'Pop3EmailMessage type is available' {
        [Mailozaurr.Pop3EmailMessage] | Should -Not -BeNullOrEmpty
    }

    It 'Pop3MessageInfo type is available' {
        [Mailozaurr.Pop3MessageInfo] | Should -Not -BeNullOrEmpty
    }

    It 'ImapEmailMessage type is available' {
        [Mailozaurr.ImapEmailMessage] | Should -Not -BeNullOrEmpty
    }

    It 'ImapMessageInfo type is available' {
        [Mailozaurr.ImapMessageInfo] | Should -Not -BeNullOrEmpty
    }

    It 'GraphMessageInfo type is available' {
        [Mailozaurr.GraphMessageInfo] | Should -Not -BeNullOrEmpty
    }

    It 'GraphEmailMessage type is available' {
        [Mailozaurr.GraphEmailMessage] | Should -Not -BeNullOrEmpty
    }
}

Describe 'Message encryption detection' {
    It 'Defaults to None for plain message' {
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Subject = 'test'
        $imap = [Mailozaurr.ImapEmailMessage]::new([MailKit.UniqueId]::new(1), $msg)
        $imap.Encryption.ToString() | Should -Be 'None'
    }
}

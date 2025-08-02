Describe 'Match-EmailDelivery' {
    It 'Throws when connection missing' {
        $repo = [Mailozaurr.FileSentMessageRepository]::new([IO.Path]::GetTempFileName())
        $resolver = [Mailozaurr.SendLogResolver]::new($repo)
        { Match-EmailDelivery -Protocol Imap -Resolver $resolver } | Should -Throw
    }
}

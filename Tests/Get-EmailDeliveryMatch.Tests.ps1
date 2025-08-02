Describe 'Get-EmailDeliveryMatch' {
    It 'Throws when connection missing' {
        $repo = [Mailozaurr.FileSentMessageRepository]::new([IO.Path]::GetTempFileName())
        $resolver = [Mailozaurr.SendLogResolver]::new($repo)
        { Get-EmailDeliveryMatch -Protocol Imap -Resolver $resolver } | Should -Throw
    }
}

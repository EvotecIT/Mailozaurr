Describe 'Get-EmailDeliveryMatch' {
    It 'Throws when connection missing' {
        $repo = [Mailozaurr.FileSentMessageRepository]::new([IO.Path]::GetTempFileName())
        $resolver = [Mailozaurr.SendLogResolver]::new($repo)
        { Get-EmailDeliveryMatch -Protocol Imap -Resolver $resolver } | Should -Throw
    }
    It 'Accepts sent log path without requiring callers to construct resolver types' {
        $path = [IO.Path]::GetTempFileName()
        { Get-EmailDeliveryMatch -Protocol Imap -SentLogPath $path } | Should -Throw -ExpectedMessage '*not provided or not connected*'
    }
    It 'Throws when POP3 connection missing' {
        $repo = [Mailozaurr.FileSentMessageRepository]::new([IO.Path]::GetTempFileName())
        $resolver = [Mailozaurr.SendLogResolver]::new($repo)
        { Get-EmailDeliveryMatch -Protocol Pop3 -Resolver $resolver } | Should -Throw
    }
    It 'Throws when Graph connection missing' {
        $repo = [Mailozaurr.FileSentMessageRepository]::new([IO.Path]::GetTempFileName())
        $resolver = [Mailozaurr.SendLogResolver]::new($repo)
        { Get-EmailDeliveryMatch -Protocol Graph -Resolver $resolver -UserPrincipalName 'user@example.com' } | Should -Throw
    }
}

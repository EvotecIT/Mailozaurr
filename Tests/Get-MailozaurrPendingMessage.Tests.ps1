Describe 'Get-MailozaurrPendingMessage' {
    It 'Lists messages from repository' {
        $path = Join-Path $TestDrive 'pending.log'
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($path)
        $msg = [MimeKit.MimeMessage]::new()
        $msg.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $msg.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $msg.Subject = 'Pending'
        $msg.Body = [MimeKit.TextPart]::new('plain')
        $ms = [System.IO.MemoryStream]::new()
        $msg.WriteTo($ms)
        $record = [Mailozaurr.PendingMessageRecord]::new()
        $record.MessageId = $msg.MessageId
        $record.MimeMessage = [Convert]::ToBase64String($ms.ToArray())
        $record.Timestamp = [DateTimeOffset]::UtcNow
        $record.NextAttemptAt = [DateTimeOffset]::UtcNow
        $repo.SaveAsync($record).GetAwaiter().GetResult()

        $result = Get-MailozaurrPendingMessage -PendingMessagesPath $path
        $result.MessageId | Should -Be $msg.MessageId
    }
}

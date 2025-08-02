Describe 'SendLogResolver' {
    It 'Resolves saved record' {
        $path = Join-Path $TestDrive 'sentlog.json'
        $repo = [Mailozaurr.FileSentMessageRepository]::new($path)
        $record = [Mailozaurr.SentMessageRecord]::new()
        $record.MessageId = 'id1'
        $record.Recipients = 'c@d.com'
        $record.Subject = 'subject'
        $record.Timestamp = [DateTimeOffset]::UtcNow
        $repo.SaveAsync($record).GetAwaiter().GetResult() | Out-Null
        $resolver = [Mailozaurr.SendLogResolver]::new($repo)
        $ndr = [Mailozaurr.NonDeliveryReports.NonDeliveryReport]::new()
        $ndr.OriginalMessageId = 'id1'
        $ndr.FinalRecipient = 'c@d.com'
        $resolved = $resolver.ResolveAsync($ndr).GetAwaiter().GetResult()
        $resolved.Subject | Should -Be 'subject'
    }
}

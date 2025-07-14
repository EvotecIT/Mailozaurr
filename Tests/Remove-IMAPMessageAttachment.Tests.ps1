Describe 'Remove-IMAPMessageAttachment' {
    It 'Removes attachments from MimeMessage' -Skip:$true {
        $path = Join-Path $TestDrive 'file.txt'
        'a' | Set-Content -Path $path
        $builder = [MimeKit.BodyBuilder]::new()
        $builder.TextBody = 'body'
        $builder.Attachments.Add($path) | Out-Null
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Body = $builder.ToMessageBody()
        ($msg.Attachments | Measure-Object).Count | Should -Be 1
        $result = Remove-IMAPMessageAttachment -Message $msg
        ($result.Attachments | Measure-Object).Count | Should -Be 0
    }
}

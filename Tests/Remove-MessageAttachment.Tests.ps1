Describe 'Remove-MessageAttachment' {
    It 'Removes attachments from MimeMessage' {
        $path = Join-Path $TestDrive 'file.txt'
        'a' | Set-Content -Path $path
        $builder = [MimeKit.BodyBuilder]::new()
        $builder.TextBody = 'body'
        $builder.Attachments.Add($path) | Out-Null
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Body = $builder.ToMessageBody()
        ($msg.Attachments | Measure-Object).Count | Should -Be 1
        $result = Remove-MessageAttachment -MimeMessage $msg
        ($result.Attachments | Measure-Object).Count | Should -Be 0
    }

    It 'Clears attachments on GraphMessage' {
        $file = Join-Path $TestDrive 'file2.txt'
        'b' | Set-Content -Path $file
        $graphMsg = [Mailozaurr.GraphMessage]::new()
        $graphMsg.Subject = 's'
        $graphMsg.Body = [Mailozaurr.GraphContent]::new()
        $graphMsg.Attachments = @([Mailozaurr.GraphAttachment]::FromFile($file))
        $result = Remove-MessageAttachment -GraphMessage $graphMsg
        $null -eq $result.Attachments | Should -BeTrue
    }
}

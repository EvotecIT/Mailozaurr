Describe 'Get-MimeMessageContent' {
    It 'Returns text and html bodies' {
        $builder = [MimeKit.BodyBuilder]::new()
        $builder.TextBody = 'plain'
        $builder.HtmlBody = '<b>html</b>'
        $msg = [MimeKit.MimeMessage]::new()
        $msg.Body = $builder.ToMessageBody()
        $content = Get-MimeMessageContent -InputObject $msg
        $content.TextBody | Should -Be 'plain'
        $content.HtmlBody | Should -Be '<b>html</b>'
    }
}

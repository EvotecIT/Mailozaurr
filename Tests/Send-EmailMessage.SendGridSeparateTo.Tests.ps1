Describe 'SendGrid SeparateTo' {
    It 'Creates separate personalizations per recipient' {
        $client = [Mailozaurr.SendGridClient]::new()
        $client.From = 'from@example.com'
        $client.To = @('a@example.com','b@example.com','a@example.com')
        $client.Cc = @('cc@example.com')
        $client.Bcc = @('b@example.com','bcc@example.com')
        $client.Subject = 'subject'
        $client.Text = 'body'
        $client.SeparateTo = $true
        $client.CreateMessage()
        $json = $client.GetType().GetProperty('MessageJson',[System.Reflection.BindingFlags] 'NonPublic,Instance').GetValue($client)
        $data = $json | ConvertFrom-Json
        $data.personalizations.Count | Should -Be 2
        foreach ($p in $data.personalizations) {
            $p.to.Count | Should -Be 1
            $p.cc.Count | Should -Be 1
            $p.bcc.Count | Should -Be 1
        }
    }
}

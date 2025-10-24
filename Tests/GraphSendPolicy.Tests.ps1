Describe 'GraphSendPolicy and Options' {
    It 'Allows setting default policy' {
        $policy = [Mailozaurr.GraphSendPolicy]::new()
        $policy.MaxConcurrency = 2
        $policy.MaxRetries = 4
        [Mailozaurr.MailozaurrOptions]::DefaultGraphPolicy = $policy
        [Mailozaurr.MailozaurrOptions]::DefaultGraphPolicy | Should -Not -BeNullOrEmpty
        [Mailozaurr.MailozaurrOptions]::DefaultGraphPolicy.MaxRetries | Should -Be 4
    }

    It 'Graph.WithSendPolicy adjusts global concurrency' {
        $g = [Mailozaurr.Graph]::new()
        $p = [Mailozaurr.GraphSendPolicy]::new()
        $p.MaxConcurrency = 3
        $null = $g.WithSendPolicy($p)
        [Mailozaurr.MicrosoftGraphUtils]::MaxConcurrentRequests | Should -Be 3
    }
}


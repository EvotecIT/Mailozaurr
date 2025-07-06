Describe 'MicrosoftGraphUtils.MaxConcurrentRequests' {
    It 'Can change concurrency limit' {
        [Mailozaurr.MicrosoftGraphUtils]::MaxConcurrentRequests = 7
        [Mailozaurr.MicrosoftGraphUtils]::MaxConcurrentRequests | Should -Be 7
    }
}

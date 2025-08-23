Describe 'Get-EmailDeliveryStatus' {
    It 'Exposes GmailApi parameter set' {
        (Get-Command Get-EmailDeliveryStatus).ParameterSets.Name | Should -Contain 'GmailApi'
    }
    It 'Includes Protocol parameter' {
        (Get-Command Get-EmailDeliveryStatus).Parameters.ContainsKey('Protocol') | Should -BeTrue
    }
    It 'Includes ParallelDownloadLimit parameter' {
        (Get-Command Get-EmailDeliveryStatus).Parameters.ContainsKey('ParallelDownloadLimit') | Should -BeTrue
    }
}


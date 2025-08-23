Describe 'Get-DmarcReport' {
    It 'Exposes GmailApi parameter set' {
        (Get-Command Get-DmarcReport).ParameterSets.Name | Should -Contain 'GmailApi'
    }
    It 'Includes Protocol parameter' {
        (Get-Command Get-DmarcReport).Parameters.ContainsKey('Protocol') | Should -BeTrue
    }
    It 'Includes Domain parameter' {
        (Get-Command Get-DmarcReport).Parameters.ContainsKey('Domain') | Should -BeTrue
    }
}

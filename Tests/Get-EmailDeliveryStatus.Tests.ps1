Describe 'Get-EmailDeliveryStatus' {
    It 'Throws when connection missing' {
        { Get-EmailDeliveryStatus -Protocol Imap } | Should -Throw
    }
}

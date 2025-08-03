Describe 'Get-EmailDeliveryStatus' {
    It 'Throws when connection missing' {
        { Get-EmailDeliveryStatus -Protocol Imap } | Should -Throw
    }
    It 'Throws when POP3 connection missing' {
        { Get-EmailDeliveryStatus -Protocol Pop3 } | Should -Throw
    }
    It 'Throws when Graph connection missing' {
        { Get-EmailDeliveryStatus -Protocol Graph -UserPrincipalName 'user@example.com' } | Should -Throw
    }

    It 'Accepts ParallelDownloadLimit parameter' {
        { Get-EmailDeliveryStatus -Protocol Imap -ParallelDownloadLimit 2 } | Should -Throw
    }
}

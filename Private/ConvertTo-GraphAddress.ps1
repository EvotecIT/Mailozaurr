function ConvertTo-GraphAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress
    )
    foreach ($_ in $MailboxAddress) {
        if ($_ -is [string]) {
            @{
                emailAddress = @{
                    address = $_
                }
            }
        } elseif ($_ -is [System.Collections.IDictionary]) {
            @{
                emailAddress = @{
                    address = $_.Email
                }
            }
        } elseif ($_ -is [MimeKit.MailboxAddress]) {
            @{
                emailAddress = @{
                    address = $_.Address
                }
            }
        } else {
            if ($_.Name -and $_.Email) {
                @{
                    emailAddress = @{
                        address = $_.Email
                    }
                }
            } elseif ($_.Email) {
                @{
                    emailAddress = @{
                        address = $_.Email
                    }
                }

            }
        }
    }
}
function ConvertTo-GraphAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress,
        [object] $From,
        [switch] $LimitedFrom
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
    if ($From) {
        if ($From -is [string]) {
            if ($LimitedFrom) {
                $From
            } else {
                @{
                    emailAddress = @{
                        address = $From
                    }
                }
            }
        } elseif ($From -is [System.Collections.IDictionary]) {
            if ($LimitedFrom) {
                $From.Email
            } else {
                @{
                    emailAddress = @{
                        address = $From.Email
                        name    = $From.Name
                    }
                }
            }
        }
    }
}
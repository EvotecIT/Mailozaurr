function ConvertTo-SendGridAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress,
        [alias('ReplyTo')][object] $From,
        [switch] $LimitedFrom
    )
    foreach ($_ in $MailboxAddress) {
        if ($_ -is [string]) {
            if ($_) {
                @{ email = $_ }
            }
        } elseif ($_ -is [System.Collections.IDictionary]) {
            if ($_.Email) {
                @{ email = $_.Email }
            }
        } elseif ($_ -is [MimeKit.MailboxAddress]) {
            if ($_.Address) {
                @{ email = $_.Address }
            }
        } else {
            if ($_.Name -and $_.Email) {
                @{
                    email = $_.Email
                    name  = $_.Name
                }
            } elseif ($_.Email) {
                @{ email = $_.Email }
            }
        }
    }
    if ($From) {
        if ($From -is [string]) {
            if ($LimitedFrom) {
                $From
            } else {
                @{ email = $From }
            }
        } elseif ($From -is [System.Collections.IDictionary]) {
            if ($LimitedFrom) {
                $From.Email
            } else {
                @{
                    email = $From.Email
                    name  = $From.Name
                }
            }
        }
    }
}
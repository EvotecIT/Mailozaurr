function ConvertTo-SendGridAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress,
        [alias('ReplyTo')][object] $From,
        [switch] $LimitedFrom
    )
    foreach ($E in $MailboxAddress) {
        if ($E -is [string]) {
            if ($E) {
                if ($E -notlike "*<*>*") {
                    @{ email = $E }
                } else {
                    # supports 'User01 <user01@fabrikam.com>'
                    $Mailbox = [MimeKit.MailboxAddress] $E
                    @{ email = $Mailbox.Address }
                }
            }
        } elseif ($E -is [System.Collections.IDictionary]) {
            if ($E.Email) {
                @{ email = $E.Email }
            }
        } elseif ($E -is [MimeKit.MailboxAddress]) {
            if ($E.Address) {
                @{ email = $E.Address }
            }
        } else {
            if ($E.Name -and $E.Email) {
                @{
                    email = $E.Email
                    name  = $E.Name
                }
            } elseif ($E.Email) {
                @{ email = $E.Email }
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
        } elseif ($From -is [MimeKit.MailboxAddress]) {
            if ($LimitedFrom) {
                $From.Address
            } else {
                @{
                    email = $From.Address
                    name  = $From.Name
                }
            }
        } else {
            if ($From.Email) {
                @{
                    email = $From.Email
                    name  = $From.Name
                }
            } elseif ($From.Email) {
                @{ email = $From.Email }
            }
        }
    }
}
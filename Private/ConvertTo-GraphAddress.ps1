function ConvertTo-GraphAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress,
        [object] $From,
        [switch] $LimitedFrom
    )
    foreach ($E in $MailboxAddress) {
        if ($E -is [string]) {
            if ($E) {
                if ($E -notlike "*<*>*") {
                    @{
                        emailAddress = @{
                            address = $E
                        }
                    }
                } else {
                    # supports 'User01 <user01@fabrikam.com>'
                    $Mailbox = [MimeKit.MailboxAddress] $E
                    @{
                        emailAddress = @{
                            address = $Mailbox.Address
                        }
                    }
                }
            }
        } elseif ($E -is [System.Collections.IDictionary]) {
            if ($E.Email) {
                @{
                    emailAddress = @{
                        address = $E.Email
                    }
                }
            }
        } elseif ($E -is [MimeKit.MailboxAddress]) {
            if ($E.Address) {
                @{
                    emailAddress = @{
                        address = $E.Address
                    }
                }
            }
        } else {
            if ($E.Name -and $E.Email) {
                @{
                    emailAddress = @{
                        address = $E.Email
                    }
                }
            } elseif ($E.Email) {
                @{
                    emailAddress = @{
                        address = $E.Email
                    }
                }

            }
        }
    }
    if ($From) {
        if ($From -is [string]) {
            if ($From -notlike "*<*>*") {
                if ($LimitedFrom) {
                    $From
                } else {
                    @{
                        emailAddress = @{
                            address = $From
                        }
                    }
                }
            } else {
                # supports 'User01 <user01@fabrikam.com>'
                $Mailbox = [MimeKit.MailboxAddress] $From
                if ($LimitedFrom) {
                    $Mailbox.Address
                } else {
                    @{
                        emailAddress = @{
                            address = $Mailbox.Address
                        }
                    }
                }
            }
        } elseif ($From -is [System.Collections.IDictionary]) {
            if ($LimitedFrom) {
                $From.Email
            } else {
                @{
                    emailAddress = @{
                        address = $From.Name
                        #name    = $From.Name
                    }
                }
            }
        } elseif ($From -is [MimeKit.MailboxAddress]) {
            if ($LimitedFrom) {
                $From.Address
            } else {
                @{
                    emailAddress = @{
                        address = $From.Address
                    }
                }
            }
        } else {
            if ($From.Email) {
                if ($LimitedFrom) {
                    $From.Email
                } else {
                    @{
                        emailAddress = @{
                            address = $From.Email
                        }
                    }
                }
            }
        }
    }
}
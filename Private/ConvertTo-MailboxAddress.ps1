function ConvertTo-MailboxAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress
    )
    foreach ($_ in $MailboxAddress) {
        if ($_ -is [string]) {
            $SmtpTo = [MimeKit.MailboxAddress]::new("$_", "$_")
        } elseif ($_ -is [System.Collections.IDictionary]) {
            $SmtpTo = [MimeKit.MailboxAddress]::new($_.Name, $_.Email)
        } elseif ($_ -is [MimeKit.MailboxAddress]) {
            $SmtpTo = $_
        } else {
            if ($_.Name -and $_.Email) {
                $SmtpTo = [MimeKit.MailboxAddress]::new($_.Name, $_.Email)
            } elseif ($_.Email) {
                $SmtpTo = [MimeKit.MailboxAddress]::new($_.Email, $_.Email)
            }
        }
        $SmtpTo
    }
}
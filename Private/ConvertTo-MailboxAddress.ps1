function ConvertTo-MailboxAddress {
    [cmdletBinding()]
    param(
        [Array] $MailboxAddress
    )
    <#
    MimeKit.MailboxAddress new(System.Text.Encoding encoding, string name, System.Collections.Generic.IEnumerable[string] route, string address)
    MimeKit.MailboxAddress new(string name, System.Collections.Generic.IEnumerable[string] route, string address)
    MimeKit.MailboxAddress new(System.Collections.Generic.IEnumerable[string] route, string address)
    MimeKit.MailboxAddress new(System.Text.Encoding encoding, string name, string address)
    MimeKit.MailboxAddress new(string name, string address)
    MimeKit.MailboxAddress new(string address)
    #>
    foreach ($_ in $MailboxAddress) {
        if ($_ -is [string]) {
            $SmtpTo = [MimeKit.MailboxAddress]::new("$_")
        } elseif ($_ -is [System.Collections.IDictionary]) {
            $SmtpTo = [MimeKit.MailboxAddress]::new($_.Name, $_.Email)
        } elseif ($_ -is [MimeKit.MailboxAddress]) {
            $SmtpTo = $_
        } else {
            if ($_.Name -and $_.Email) {
                $SmtpTo = [MimeKit.MailboxAddress]::new($_.Name, $_.Email)
            } elseif ($_.Email) {
                $SmtpTo = [MimeKit.MailboxAddress]::new($_.Email)
            }
        }
        $SmtpTo
    }
}
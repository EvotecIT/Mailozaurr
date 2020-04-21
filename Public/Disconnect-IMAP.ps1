function Disconnect-IMAP {
    [cmdletBinding()]
    param(
        [System.Collections.IDictionary] $Client
    )
    if ($Client.Data) {
        try {
            $Client.Data.Disconnect($true)
        } catch {
            Write-Warning "Disconnect-IMAP - Unable to authenticate $($_.Exception.Message)"
            return
        }
    }
}
function Disconnect-POP {
    [alias('Disconnect-POP3')]
    [cmdletBinding()]
    param(
        [System.Collections.IDictionary] $Client
    )
    if ($Client.Data) {
        try {
            $Client.Data.Disconnect($true)
        } catch {
            Write-Warning "Disconnect-POP - Unable to authenticate $($_.Exception.Message)"
            return
        }
    }
}
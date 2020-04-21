function Save-POPMessage {
    [alias('Save-POP3Message')]
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Client,
        [Parameter(Mandatory)][int] $Index,
        [Parameter(Mandatory)][string] $Path #,
        # [int] $Count = 1,
        #[switch] $All
    )
    if ($Client -and $Client.Data) {
        if ($All) {
            # $Client.Data.GetMessages($Index, $Count)
        } else {
            if ($Index -lt $Client.Data.Count) {
                $Client.Data.GetMessage($Index).WriteTo($Path)
            } else {
                Write-Warning "Save-POP3Message - Index is out of range. Use index less than $($Client.Data.Count)."
            }
        }
    }
}
function Resolve-DnsQuery {
    [cmdletBinding()]
    param(
        [alias('Query')][Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][DnsClient.QueryType] $Type,
        [switch] $All
    )
    $Lookup = [DnsClient.LookupClient]::new()
    if ($Type -eq [DnsClient.QueryType]::PTR) {
        #$Lookup = [DnsClient.LookupClient]::new()
        $Results = $Lookup.QueryReverseAsync($Name) | Wait-Task
        $Name = $Results.Answers.DomainName.Original
    }
    $Results = $Lookup.Query($Name, $Type)
    if ($All) {
        $Results
    } else {
        $Results.Answers
    }
}
function Resolve-DnsQuery {
    [cmdletBinding()]
    param(
        [alias('Query')][Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][DnsClient.QueryType] $Type,
        [string] $Server,
        [switch] $All
    )
    if ($Server) {
        if ($Server -like '*:*') {
            $SplittedServer = $Server.Split(':')
            [System.Net.IPAddress] $IpAddress = $SplittedServer[0]
            $EndPoint = [System.Net.IPEndPoint]::new($IpAddress, $SplittedServer[1]) ##(IPAddress.Parse("127.0.0.1"), 8600);
        } else {
            [System.Net.IPAddress] $IpAddress = $Server
            $EndPoint = [System.Net.IPEndPoint]::new($IpAddress, 53) ##(IPAddress.Parse("127.0.0.1"), 8600);
        }
        $Lookup = [DnsClient.LookupClient]::new($EndPoint)
    } else {
        $Lookup = [DnsClient.LookupClient]::new()
    }
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
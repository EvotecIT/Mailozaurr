function Find-DNSBL {
    <#
    .SYNOPSIS
    Searches DNSBL if particular IP is blocked on DNSBL.

    .DESCRIPTION
    Searches DNSBL if particular IP is blocked on DNSBL.

    .PARAMETER IP
    IP to check if it exists on DNSBL

    .PARAMETER BlockListServers
    Provide your own blocklist of servers

    .PARAMETER All
    Return All entries. By default it returns only those on DNSBL.

    .PARAMETER DNSServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .EXAMPLE
    Find-DNSBL -IP '89.74.48.96' | Format-Table

    .EXAMPLE
    Find-DNSBL -IP '89.74.48.96', '89.74.48.97', '89.74.48.98' | Format-Table

    .EXAMPLE
    Find-DNSBL -IP '89.74.48.96' -DNSServer 1.1.1.1 | Format-Table

    .EXAMPLE
    Find-DNSBL -IP '89.74.48.96' -DNSProvider Cloudflare | Format-Table

    .NOTES
    General notes
    #>
    [alias('Find-BlackList', 'Find-BlockList')]
    [cmdletBinding(DefaultParameterSetName = 'Default')]
    param(
        [Parameter(Mandatory)][string[]] $IP,
        [string[]] $BlockListServers = $Script:BlockList,
        [switch] $All,
        [Parameter(ParameterSetName = 'DNSServer')][string] $DNSServer,
        [Parameter(ParameterSetName = 'DNSProvider')][ValidateSet('Cloudflare', 'Google')][string] $DNSProvider
    )
    foreach ($I in $IP) {
        foreach ($Server in $BlockListServers) {
            [string] $FQDN = $I -replace '^(\d+)\.(\d+)\.(\d+)\.(\d+)$', "`$4.`$3.`$2.`$1.$Server"
            if (-not $DNSProvider) {
                $DnsQuery = Resolve-DnsQuery -Name $FQDN -Type A -Server $DNSServer -All
                $Answer = $DnsQuery.Answers[0].Address.IPAddressToString
                $IsListed = $null -ne $Answer
            } else {
                $DnsQuery = Resolve-DnsQueryRest -Name $FQDN -Type A -DNSProvider $DNSProvider -All
                $Answer = $DnsQuery.Answers.Address
                $IsListed = $null -ne $DnsQuery.Answers
            }
            $Result = [PSCustomObject] @{
                IP         = $I
                FQDN       = $FQDN
                BlackList  = $Server
                IsListed   = $IsListed
                Answer     = $Answer
                TTL        = $DnsQuery.Answers.TimeToLive
                NameServer = $DnsQuery.NameServer
            }
            if (-not $All -and $Result.IsListed -eq $false) {
                continue
            }
            $Result
        }
    }
}

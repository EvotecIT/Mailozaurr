function Resolve-DnsQueryRest {
    <#
    .SYNOPSIS
    Short description

    .DESCRIPTION
    Long description

    .PARAMETER DNSProvider
    Parameter description

    .PARAMETER Name
    Parameter description

    .PARAMETER Type
    Parameter description

    .PARAMETER All
    Parameter description

    .EXAMPLE
    Resolve-DnsQueryRest -Name 'evotec.pl' -Type TXT -DNSProvider Cloudflare

    .NOTES
    General notes
    #>
    [cmdletBinding()]
    param(
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider = 'Cloudflare',
        [alias('Query')][Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][DnsQueryType] $Type,
        [switch] $All
    )
    if ($DNSProvider -eq 'Cloudflare') {
        $Q = Invoke-RestMethod -Uri "https://cloudflare-dns.com/dns-query?ct=application/dns-json&name=$Name&type=$Type"
    } else {
        $Q = Invoke-RestMethod -Uri "https://dns.google.com/resolve?name=$Name&type=$Type"
    }
    $Answers = foreach ($Answer in $Q.Answer) {
        [PSCustomObject] @{
            Name       = $Answer.Name
            Count      = $Answer.Type
            TimeToLive = $Answer.TTL
            Text       = $Answer.data
        }
    }
    if ($All) {
        [PSCustomObject] @{
            NameServer = if ($DNSProvider -eq 'Cloudflare') { 'cloudflare-dns.com' } else { 'dns.google.com' }
            Answers    = $Answers
        }
    } else {
        $Answers
    }
}
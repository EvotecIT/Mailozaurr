function Resolve-DnsQueryRest {
    <#
    .SYNOPSIS
    Provides basic DNS Query via HTTPS

    .DESCRIPTION
    Provides basic DNS Query via HTTPS - tested only for use cases within Mailozaurr

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google). Default is Cloudflare

    .PARAMETER Name
    Name/DomainName to query DNS

    .PARAMETER Type
    Type of a query A, PTR, MX and so on

    .PARAMETER All
    Returns full output rather than just custom, translated data

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
    if ($Type -eq [DnsQueryType]::PTR) {
        $Name = $Name -replace '^(\d+)\.(\d+)\.(\d+)\.(\d+)$', '$4.$3.$2.$1.in-addr.arpa'
    }
    if ($DNSProvider -eq 'Cloudflare') {
        $Q = Invoke-RestMethod -Uri "https://cloudflare-dns.com/dns-query?ct=application/dns-json&name=$Name&type=$Type"
    } else {
        $Q = Invoke-RestMethod -Uri "https://dns.google.com/resolve?name=$Name&type=$Type"
    }
    $Answers = foreach ($Answer in $Q.Answer) {
        if ($Type -eq [DnsQueryType]::MX) {
            $Data = $Answer.data -split ' '
            [PSCustomObject] @{
                Name       = $Answer.Name
                Count      = $Answer.Type
                TimeToLive = $Answer.TTL
                Exchange   = $Data[1]
                Preference = $Data[0]
            }
        } else {
            [PSCustomObject] @{
                Name       = $Answer.Name
                Count      = $Answer.Type
                TimeToLive = $Answer.TTL
                Text       = $Answer.data.TrimStart('"').TrimEnd('"')
            }
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
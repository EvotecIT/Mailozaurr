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
    [alias('Resolve-DnsRestQuery')]
    [cmdletBinding()]
    param(
        [alias('Query')][Parameter(Mandatory, Position = 0)][string] $Name,
        [Parameter(Mandatory, Position = 1)][string] $Type,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider = 'Cloudflare',
        [switch] $All
    )
    if ($Type -eq 'PTR') {
        $Name = $Name -replace '^(\d+)\.(\d+)\.(\d+)\.(\d+)$', '$4.$3.$2.$1.in-addr.arpa'
    }
    if ($DNSProvider -eq 'Cloudflare') {
        $Q = Invoke-RestMethod -Uri "https://cloudflare-dns.com/dns-query?name=$Name&type=$Type" -Headers @{ accept = 'application/dns-json' }
    } else {
        $Q = Invoke-RestMethod -Uri "https://dns.google.com/resolve?name=$Name&type=$Type"
    }
    $Answers = foreach ($Answer in $Q.Answer) {
        if ($Type -eq 'MX') {
            $Data = $Answer.data -split ' '
            [PSCustomObject] @{
                Name       = $Answer.Name
                Count      = $Answer.Type
                TimeToLive = $Answer.TTL
                Exchange   = $Data[1]
                Preference = $Data[0]
            }
        } elseif ($Type -eq 'A') {
            [PSCustomObject] @{
                Name       = $Answer.Name
                Count      = $Answer.Type
                TimeToLive = $Answer.TTL
                Address    = $Answer.data #.TrimStart('"').TrimEnd('"')
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
        # TC	If true, it means the truncated bit was set. This happens when the DNS answer is larger than a single UDP or TCP packet. TC will almost always be false with Cloudflare DNS over HTTPS because Cloudflare supports the maximum response size.
        # RD	If true, it means the Recursive Desired bit was set. This is always set to true for Cloudflare DNS over HTTPS.
        # RA	If true, it means the Recursion Available bit was set. This is always set to true for Cloudflare DNS over HTTPS.
        # AD	If true, it means that every record in the answer was verified with DNSSEC.
        # CD	If true, the client asked to disable DNSSEC validation. In this case, Cloudflare will still fetch the DNSSEC-related records, but it will not attempt to validate

        $Output = [ordered] @{}
        foreach ($Name in $Q.PSObject.Properties.Name) {
            if ($Name -eq 'Answer') { continue }
            $Output[$Name] = $Q.$Name
        }
        $Output['NameServer'] = if ($DNSProvider -eq 'Cloudflare') { 'cloudflare-dns.com' } else { 'dns.google.com' }
        $Output['Answers'] = $Answers
        [PSCustomObject] $Output

    } else {
        $Answers
    }
}

Register-ArgumentCompleter -CommandName Resolve-DnsQueryRest -ParameterName Type -ScriptBlock $Script:DNSQueryTypes
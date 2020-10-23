function Find-DKIMRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide DKIM information

    .DESCRIPTION
    Queries DNS to provide DKIM information

    .PARAMETER DomainName
    Name/DomainName to query for DKIM record

    .PARAMETER Selector
    Selector name. Default: selector1

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .PARAMETER AsHashTable
    Returns Hashtable instead of PSCustomObject

    .PARAMETER AsObject
    Returns an object rather than string based represantation for name servers (for easier display purposes)

    .EXAMPLE
    # Standard way
    Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

    .EXAMPLE
    # Https way via Cloudflare
    Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

    .EXAMPLE
    # Https way via Google
    Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -Selector 'selector1' -DNSProvider Google | Format-Table *

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
        [string] $Selector = 'selector1',
        [string] $DnsServer,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $S = $Selector
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $S = $Domain.Selector
                $D = $Domain.DomainName
                if (-not $S -and -not $D) {
                    Write-Warning 'Find-DKIMRecord - properties DomainName and Selector are required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "$S._domainkey.$D"
                Type        = 'TXT'
                ErrorAction = 'SilentlyContinue'
            }
            if ($DNSProvider) {
                $DNSRecord = Resolve-DnsQueryRest @Splat -All -DNSProvider $DnsProvider
            } else {
                if ($DnsServer) {
                    $Splat['Server'] = $DnsServer
                }
                $DNSRecord = Resolve-DnsQuery @Splat -All
            }
            $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'DKIM1'
            if (-not $AsObject) {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Count       = $DNSRecordAnswers.Text.Count
                    Selector    = "$D`:$S"
                    DKIM        = $DNSRecordAnswers.Text -join '; '
                    QueryServer = $DNSRecord.NameServer
                }
            } else {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Count       = $DNSRecordAnswers.Text.Count
                    Selector    = "$D`:$S"
                    DKIM        = $DNSRecordAnswers.Text -join '; '
                    QueryServer = $DNSRecord.NameServer -join '; '
                }
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}
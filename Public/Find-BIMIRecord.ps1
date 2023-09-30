function Find-BIMIRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide BIMI information

    .DESCRIPTION
    Queries DNS to provide BIMI information

    .PARAMETER DomainName
    Name/DomainName to query for DMARC record

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .PARAMETER AsHashTable
    Returns Hashtable instead of PSCustomObject

    .PARAMETER AsObject
    Returns an object rather than string based represantation for name servers (for easier display purposes)

    .EXAMPLE
    Find-BIMIRecord -DomainName 'mxtoolbox.com' -DNSProvider Google | Format-Table

    .EXAMPLE
    Find-BIMIRecord -DomainName 'google.com' -DNSProvider Cloudflare | Format-Table

    .EXAMPLE
    Find-BIMIRecord -DomainName 'mxtoolbox.com' | Format-Table

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
        [string] $DnsServer,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $D = $Domain.DomainName
                if (-not $D) {
                    Write-Warning -Message 'Find-BIMIRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "default._bimi.$D"
                Type        = 'TXT'
                ErrorAction = 'Stop'
            }
            try {
                if ($DNSProvider) {
                    $DNSRecord = Resolve-DnsQueryRest @Splat -All -DNSProvider $DnsProvider
                } else {
                    if ($DnsServer) {
                        $Splat['Server'] = $DnsServer
                    }
                    $DNSRecord = Resolve-DnsQuery @Splat -All
                }
                [Array] $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'v=BIMI1;'
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive -join '; '
                        BIMI        = $DNSRecordAnswers.Text -join '; '
                        QueryServer = $DNSRecord.NameServer -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive
                        BIMI        = $DNSRecordAnswers.Text
                        QueryServer = $DNSRecord.NameServer
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Count       = 0
                    TimeToLive  = ''
                    TLSRPT      = ''
                    QueryServer = ''
                }
                Write-Warning -Message "Find-BIMIRecord - $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}


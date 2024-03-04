function Find-TLSRPTRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide TLS-RPT information

    .DESCRIPTION
    Queries DNS to provide TLS-RPT information

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
    Find-TLSRPTRecord -DomainName 'evotec.xyz' -DNSProvider Cloudflare

    .EXAMPLE
    Find-TLSRPTRecord -DomainName 'evotec.xyz' -DNSProvider

    .EXAMPLE
    Find-TLSRPTRecord -DomainName 'evotec.xyz'

    .NOTES
    SMTP TLS Reporting (TLS-RPT) is a standard that enables the reporting of issues in TLS connectivity
    that is experienced by applications that send emails and detect misconfigurations.
    It enables the reporting of email delivery issues that take place when an email isn't encrypted with TLS.
    In September 2018 the standard was first documented in RFC 8460.

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
                    Write-Warning 'Find-TLSRPTRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "_smtp._tls.$D"
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
                [Array] $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'TLSRPTv1'
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive -join '; '
                        TLSRPT      = $DNSRecordAnswers.Text -join '; '
                        QueryServer = $DNSRecord.NameServer -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive
                        TLSRPT      = $DNSRecordAnswers.Text
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
                Write-Warning "Find-TLSRPTRecord - $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}


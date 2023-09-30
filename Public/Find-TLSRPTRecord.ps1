function Find-TLSRPTRecord {
    <#
    .SYNOPSIS
    Short description

    .DESCRIPTION


    .PARAMETER DomainName
    Parameter description

    .PARAMETER DnsServer
    Parameter description

    .PARAMETER DNSProvider
    Parameter description

    .PARAMETER AsHashTable
    Parameter description

    .PARAMETER AsObject
    Parameter description

    .EXAMPLE
    An example

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


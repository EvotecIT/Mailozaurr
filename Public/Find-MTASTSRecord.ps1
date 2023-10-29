function Find-MTASTSRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide MTA-STS information, and verifies if configuration exists

    .DESCRIPTION
    Queries DNS to provide MTA-STS information, and verifies if configuration exists

    .PARAMETER DomainName
    Name/DomainName to query for MTA-STS record

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .PARAMETER AsHashTable
    Returns Hashtable instead of PSCustomObject

    .PARAMETER AsObject
    Returns an object rather than string based represantation for name servers (for easier display purposes)

    .EXAMPLE
    Find-MTASTSRecord -DomainName 'google.com' -DNSProvider Google | Format-Table

    .EXAMPLE
    Find-MTASTSRecord -DomainName 'google.com' -DNSProvider Cloudflare | Format-Table

    .EXAMPLE
    Find-MTASTSRecord -DomainName 'google.com' | Format-Table

    .EXAMPLE
    Find-MTASTSRecord -DomainName 'evotec.xyz' | Format-Table

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
                    Write-Warning -Message 'Find-MTASTSRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "_mta-sts.$D"
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
                [Array] $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'v=STSv1;'

                $Version = $null
                $MxRecords = [System.Collections.Generic.List[string]]::new()
                $Mode = $null
                $MaxAge = $null

                if ($DNSRecordAnswers.Count -eq 1) {
                    $Url = "https://mta-sts.$D/.well-known/mta-sts.txt"
                    try {
                        $Response = Invoke-RestMethod -Uri $Url -ErrorAction Stop
                        $ResponseData = $Response.Trim() -split "`r`n"
                        foreach ($Data in $ResponseData) {
                            if ($Data.StartsWith("version: ")) {
                                $Version = $Data.Substring(9)
                            } elseif ($Data.StartsWith("mode: ")) {
                                $Mode = $Data.Substring(6)
                            } elseif ($Data.StartsWith("mx: ")) {
                                $MxRecords.Add($Data.Substring(4))
                            } elseif ($Data.StartsWith("max_age: ")) {
                                $MaxAge = $Data.Substring(9)
                            }
                        }
                    } catch {
                        Write-Warning -Message "Find-MTASTSRecord - Error when getting data from $($Url): $_"
                    }
                } elseif ($DNSRecordAnswers.Count -gt 1) {
                    Write-Warning -Message "Find-MTASTSRecord - More than one MTA-STS record found for $D, verification skipped"
                }
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        MTASTS      = $DNSRecordAnswers.Text -join '; '
                        Version     = $Version
                        Mode        = $Mode
                        MxRecords   = $MxRecords
                        MaxAge      = $MaxAge
                        TimeToLive  = $DNSRecordAnswers.TimeToLive -join '; '

                        QueryServer = $DNSRecord.NameServer -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        MTASTS      = $DNSRecordAnswers.Text
                        Version     = $Version
                        Mode        = $Mode
                        MxRecords   = $MxRecords
                        MaxAge      = $MaxAge
                        TimeToLive  = $DNSRecordAnswers.TimeToLive
                        QueryServer = $DNSRecord.NameServer
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name        = $D
                    MTASTS      = ""
                    Version     = ""
                    Mode        = ""
                    MxRecords   = ""
                    MaxAge      = ""
                    TimeToLive  = ''
                    QueryServer = ''
                }
                Write-Warning -Message "Find-MTASTSRecord - Error: $($_.Exception.Message)"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}


function Find-CAARecord {
    <#
    .SYNOPSIS
    Queries DNS to provide CAA information

    .DESCRIPTION
    Queries DNS to provide CAA information

    .PARAMETER DomainName
    Name/DomainName to query for CAA record

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .EXAMPLE
    Find-CAARecord -DomainName "evotec.pl" -DNSProvider Cloudflare | Format-Table -AutoSize
    Find-CAARecord -DomainName 'evotec.pl' -DNSProvider Google | Format-Table -AutoSize
    Find-CAARecord -DomainName 'evotec.pl' -Verbose | Format-Table -AutoSize

    .EXAMPLE
    Find-CAARecord -DomainName "mbank.pl" -DNSProvider Cloudflare | Format-Table -AutoSize
    Find-CAARecord -DomainName 'mbank.pl' -DNSProvider Google | Format-Table -AutoSize
    Find-CAARecord -DomainName 'mbank.pl' -Verbose | Format-Table -AutoSize

    .NOTES
    We try to follow rfc, but the key/value pair may not be correct. If you find any issues, please report them.

    The flag must be a number between 0 and 255, 0 being the most commonly used value.
    The tag must be one of issue, issuewild, or iodef.
    The value part:
    - It must be wrapped between double quotes ".
    - There are no length restrictions on this part.
    - Any inner double quotes " must be escaped with the \" character sequence.
    - Based on the specific tag value, it must follow the extra rules described below:

    issue and issuewild tag value
    - It must contain a domain name.
    - The domain name can be followed by a list of parameters with the following pattern:

    - 0 issue "letsencrypt.com;key1=value1;key2=value2"
    - The domain name can also be left empty, which must be indicated providing just ";" as a value:

    - 0 issue ";"
    iodef tag value
    - It must contain a URL.
    - The provided URL must have one of the following schemes: mailto, http, or https.
    - If the URL has the mailto scheme, it must conform to an email URL, like mailto:admin@example.com.
    - If the URL has the http or https schemes, it must be a valid HTTP/HTTPS URL, like https://dnsimple.com/report_caa.
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
                    Write-Warning -Message 'Find-CAARecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "$D"
                Type        = 'CAA'
                ErrorAction = 'Stop'
            }
            try {
                if ($DNSProvider) {
                    $DNSRecord = Resolve-DnsQueryRest @Splat -All -DNSProvider $DnsProvider
                } else {
                    if ($DnsServer) {
                        $Splat['Server'] = $DnsServer
                    }
                    $DNSRecord = Resolve-DnsQuery @Splat -All -Verbose
                }
                [Array] $DNSRecordAnswers = $DNSRecord.Answers
                foreach ($Record in $DNSRecordAnswers) {
                    if ($DNSProvider -eq 'Cloudflare') {
                        $Data = Convert-CloudflareToGoogleCAA -hexString $Record.Text
                        $NewData = $Data -split ' '
                        $Flags = $NewData[0]
                        $Tag = $NewData[1]
                        $DomainValue = $NewData[2].Replace('"', '').Replace(';', '')
                        $cansignhttpexchanges = $false
                        if ($NewData[3] -match 'cansignhttpexchanges') {
                            $cansignhttpexchangesTemp = $NewData[3] -replace 'cansignhttpexchanges=', ''
                            if ($cansignhttpexchangesTemp -eq 'yes') {
                                $cansignhttpexchanges = $true
                            } else {
                                $cansignhttpexchanges = $false
                            }
                        }
                        [PSCustomObject] @{
                            DomainName           = $D
                            Flags                = $Flags
                            Tag                  = $Tag
                            DomainValue          = $DomainValue
                            CanSignHttpExchanges = $cansignhttpexchanges
                            TimeToLive           = $Record.TimeToLive
                        }

                    } elseif ($DNSProvider -eq 'Google') {
                        $Data = $Record.Text
                        $NewData = $Data -split ' '
                        $Flags = $NewData[0]
                        $Tag = $NewData[1]
                        $DomainValue = $NewData[2].Replace('"', '').Replace(';', '')
                        $cansignhttpexchanges = $false
                        if ($NewData[3] -match 'cansignhttpexchanges') {
                            $cansignhttpexchangesTemp = $NewData[3] -replace 'cansignhttpexchanges=', ''
                            if ($cansignhttpexchangesTemp -eq 'yes') {
                                $cansignhttpexchanges = $true
                            } else {
                                $cansignhttpexchanges = $false
                            }
                        }
                        [PSCustomObject] @{
                            DomainName           = $D
                            Flags                = $Flags
                            Tag                  = $Tag
                            DomainValue          = $DomainValue
                            CanSignHttpExchanges = $cansignhttpexchanges
                            TimeToLive           = $Record.TimeToLive
                        }

                    } else {
                        $Flags = $Record.Flags
                        $Tag = $Record.Tag
                        $Data = $Record.Value.Replace('\;', ';')
                        $NewData = $Data -split ';'
                        $DomainValue = $NewData[0].Trim()
                        # digicert.com\; cansignhttpexchanges=yes
                        if ($NewData[1] -match 'cansignhttpexchanges') {
                            $cansignhttpexchangesTemp = $NewData[1].Trim() -replace 'cansignhttpexchanges=', ''
                            if ($cansignhttpexchangesTemp -eq 'yes') {
                                $cansignhttpexchanges = $true
                            } else {
                                $cansignhttpexchanges = $false
                            }
                        } else {
                            $cansignhttpexchanges = $false
                        }
                        [PSCustomObject] @{
                            DomainName           = $D
                            Flags                = $Flags
                            Tag                  = $Tag
                            DomainValue          = $DomainValue
                            CanSignHttpExchanges = $cansignhttpexchanges
                            TimeToLive           = $Record.TimeToLive
                        }
                    }
                }
                # if there are no records we return empty object
                if ($DNSRecordAnswers.Count -eq 0) {
                    [PSCustomObject] @{
                        DomainName           = $D
                        Flags                = ''
                        Tag                  = ''
                        DomainValue          = ''
                        CanSignHttpExchanges = $null
                        TimeToLive           = $null
                    }
                }
            } catch {
                [PSCustomObject] @{
                    DomainName           = $D
                    Flags                = ''
                    Tag                  = ''
                    DomainValue          = ''
                    CanSignHttpExchanges = $null
                    TimeToLive           = $null
                }
                Write-Warning -Message "Find-CAARecord - Error: $($_.Exception.Message)"
            }
        }
    }
}
function Find-MxRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide MX information

    .DESCRIPTION
    Queries DNS to provide MX information

    .PARAMETER DomainName
    Name/DomainName to query for MX record

    .PARAMETER ResolvePTR
    Parameter description

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .PARAMETER AsHashTable
    Returns Hashtable instead of PSCustomObject

    .PARAMETER AsObject
    Returns an object rather than string based represantation for name servers (for easier display purposes)

    .PARAMETER Separate
    Returns each MX record separatly

    .EXAMPLE
    # Standard way
    Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

    .EXAMPLE
    # Https way via Cloudflare
    Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

    .EXAMPLE
    # Https way via Google
    Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *

    .EXAMPLE
    # Standard way with ResolvePTR
    Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -ResolvePTR | Format-Table *

    .EXAMPLE
    # Https way via Cloudflare with ResolvePTR
    Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare -ResolvePTR | Format-Table *

    .EXAMPLE
    # Https way via Google with ResolvePTR
    Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google -ResolvePTR | Format-Table *

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array]$DomainName,
        [string] $DnsServer,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider,
        [switch] $ResolvePTR,
        [switch] $AsHashTable,
        [switch] $Separate,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $D = $Domain.DomainName
                if (-not $D) {
                    Write-Warning 'Find-MxRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = $D
                Type        = 'MX'
                ErrorAction = 'SilentlyContinue'
            }
            if ($DNSProvider) {
                $MX = Resolve-DnsQueryRest @Splat -All -DNSProvider $DnsProvider
            } else {
                if ($DnsServer) {
                    $Splat['Server'] = $DnsServer
                }
                $MX = Resolve-DnsQuery @Splat -All
            }
            [Array] $MXRecords = foreach ($MXRecord in $MX.Answers) {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Preference  = $MXRecord.Preference
                    TimeToLive  = $MXRecord.TimeToLive
                    MX          = ($MXRecord.Exchange) -replace '.$'
                    QueryServer = $MX.NameServer
                }
                [Array] $IPAddresses = foreach ($Record in $MX.Answers.Exchange) {
                    $Splat = @{
                        Name        = $Record
                        Type        = 'A'
                        ErrorAction = 'SilentlyContinue'
                    }
                    if ($DNSProvider) {
                        (Resolve-DnsQueryRest @Splat -DNSProvider $DnsProvider) | ForEach-Object { $_.Text }
                    } else {
                        if ($DnsServer) {
                            $Splat['Server'] = $DnsServer
                        }
                        (Resolve-DnsQuery @Splat) | ForEach-Object { $_.Address.IPAddressToString }
                    }
                }
                $MailRecord['IPAddress'] = $IPAddresses
                if ($ResolvePTR) {
                    $MailRecord['PTR'] = foreach ($IP in $IPAddresses) {
                        $Splat = @{
                            Name        = $IP
                            Type        = 'PTR'
                            ErrorAction = 'SilentlyContinue'
                        }
                        if ($DNSProvider) {
                            (Resolve-DnsQueryRest @Splat -DNSProvider $DnsProvider) | ForEach-Object { $_.Text -replace '.$' }
                        } else {
                            if ($DnsServer) {
                                $Splat['Server'] = $DnsServer
                            }
                            (Resolve-DnsQuery @Splat) | ForEach-Object { $_.PtrDomainName -replace '.$' }
                        }
                    }
                }
                $MailRecord
            }
            if ($Separate) {
                foreach ($MXRecord in $MXRecords) {
                    if ($AsHashTable) {
                        $MXRecord
                    } else {
                        [PSCustomObject] $MXRecord
                    }
                }
            } else {
                if (-not $AsObject) {
                    $MXRecord = [ordered] @{
                        Name        = $D
                        Count       = $MXRecords.Count
                        Preference  = $MXRecords.Preference -join '; '
                        TimeToLive  = $MXRecords.TimeToLive -join '; '
                        MX          = $MXRecords.MX -join '; '
                        IPAddress   = ($MXRecords.IPAddress | Sort-Object -Unique) -join '; '
                        QueryServer = $MXRecords.QueryServer -join '; '
                    }
                    if ($ResolvePTR) {
                        $MXRecord['PTR'] = ($MXRecords.PTR | Sort-Object -Unique) -join '; '
                    }
                } else {
                    $MXRecord = [ordered] @{
                        Name        = $D
                        Count       = $MXRecords.Count
                        Preference  = $MXRecords.Preference
                        TimeToLive  = $MXRecords.TimeToLive
                        MX          = $MXRecords.MX
                        IPAddress   = ($MXRecords.IPAddress | Sort-Object -Unique)
                        QueryServer = $MXRecords.QueryServer
                    }
                    if ($ResolvePTR) {
                        $MXRecord['PTR'] = ($MXRecords.PTR | Sort-Object -Unique)
                    }
                }
                if ($AsHashTable) {
                    $MXRecord
                } else {
                    [PSCustomObject] $MXRecord
                }
            }
        }
    }
}
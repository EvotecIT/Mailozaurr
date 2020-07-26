function Find-MxRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][string[]]$DomainName,
        [System.Net.IPAddress] $DnsServer,
        [switch] $ResolvePTR,
        [switch] $AsHashTable,
        [switch] $Separate,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            $Splat = @{
                Name        = $Domain
                Type        = "MX"
                ErrorAction = "SilentlyContinue"
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            $MX = Resolve-DnsQuery @Splat
            [Array] $MXRecords = foreach ($MXRecord in $MX) {
                $MailRecord = [ordered] @{
                    Name       = $Domain
                    Preference = $MXRecord.Preference
                    TimeToLive = $MXRecord.TimeToLive
                    MX         = ($MXRecord.Exchange) -replace ".$"
                }
                [Array] $IPAddresses = foreach ($Record in $MX.Exchange) {
                    $Splat = @{
                        Name        = $Record
                        Type        = 'A'
                        ErrorAction = "SilentlyContinue"
                    }
                    if ($DnsServer) {
                        $Splat['Server'] = $DnsServer
                    }
                    (Resolve-DnsQuery @Splat) | ForEach-Object { $_.Address.IPAddressToString }
                }
                $MailRecord['IPAddress'] = $IPAddresses
                if ($ResolvePTR) {
                    $MailRecord['PTR'] = foreach ($IP in $IPAddresses) {
                        $Splat = @{
                            Name        = $IP
                            Type        = 'PTR'
                            ErrorAction = "SilentlyContinue"
                        }
                        if ($DnsServer) {
                            $Splat['Server'] = $DnsServer
                        }
                        (Resolve-DnsQuery @Splat) | ForEach-Object { $_.PtrDomainName -replace ".$" }
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
                        Name       = $Domain
                        Count      = $MXRecords.Count
                        Preference = $MXRecords.Preference -join '; '
                        TimeToLive = $MXRecords.TimeToLive -join '; '
                        MX         = $MXRecords.MX -join '; '
                        IPAddress  = ($MXRecords.IPAddress | Sort-Object -Unique) -join '; '
                    }
                    if ($ResolvePTR) {
                        $MXRecord['PTR'] = ($MXRecords.PTR | Sort-Object -Unique) -join '; '
                    }
                } else {
                    $MXRecord = [ordered] @{
                        Name       = $Domain
                        Count      = $MXRecords.Count
                        Preference = $MXRecords.Preference
                        TimeToLive = $MXRecords.TimeToLive
                        MX         = $MXRecords.MX
                        IPAddress  = ($MXRecords.IPAddress | Sort-Object -Unique)
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
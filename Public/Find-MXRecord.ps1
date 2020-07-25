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
            $MX = Resolve-DnsName @Splat | Where-Object Type -EQ "MX"
            [Array] $MXRecords = foreach ($MXRecord in $MX) {
                $MailRecord = [ordered] @{
                    Name       = $Domain
                    Preference = $MXRecord.Preference
                    TTL        = $MXRecord.TTL
                    MX         = $MXRecord.NameExchange
                }
                [Array] $IPAddresses = foreach ($Record in $MX.NameExchange) {
                    $Splat = @{
                        Name        = $Record
                        Type        = 'A_AAAA'
                        ErrorAction = "SilentlyContinue"
                    }
                    if ($DnsServer) {
                        $Splat['Server'] = $DnsServer
                    }
                    (Resolve-DnsName @Splat).IPAddress
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
                        (Resolve-DnsName @Splat).NameHost
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
                        TTL        = $MXRecords.TTL -join '; '
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
                        TTL        = $MXRecords.TTL
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
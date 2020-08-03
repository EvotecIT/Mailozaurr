function Find-SPFRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array]$DomainName,
        [System.Net.IPAddress] $DnsServer,
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
                    Write-Warning 'Find-MxRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = $D
                Type        = 'txt'
                ErrorAction = 'Stop'
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            try {
                $DNSRecord = Resolve-DnsQuery @Splat | Where-Object Text -Match 'spf1'
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name       = $D
                        Count      = $DNSRecord.Count
                        TimeToLive = $DnsRecord.TimeToLive -join '; '
                        SPF        = $DnsRecord.Text -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name       = $D
                        Count      = $DNSRecord.Count
                        TimeToLive = $DnsRecord.TimeToLive
                        SPF        = $DnsRecord.Text
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name       = $D
                    Count      = 0
                    TimeToLive = ''
                    SPF        = ''
                }
                Write-Warning "Find-SPFRecord - $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}
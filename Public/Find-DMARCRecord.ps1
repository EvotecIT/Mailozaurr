function Find-DMARCRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
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
                    Write-Warning 'Find-DMARCRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "_dmarc.$D"
                Type        = 'TXT'
                ErrorAction = 'Stop'
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            try {
                $DNSRecord = Resolve-DnsQuery @Splat | Where-Object Text -Match 'DMARC1'
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name       = $D
                        Count      = $DNSRecord.Count
                        TimeToLive = $DnsRecord.TimeToLive -join '; '
                        DMARC      = $DnsRecord.Text -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name       = $D
                        Count      = $DNSRecord.Count
                        TimeToLive = $DnsRecord.TimeToLive
                        DMARC      = $DnsRecord.Text
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name       = $D
                    Count      = 0
                    TimeToLive = ''
                    DMARC      = ''
                }
                Write-Warning "Find-DMARCRecord - $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}